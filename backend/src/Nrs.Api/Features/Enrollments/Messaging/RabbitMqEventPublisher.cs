using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// Publishes enrollment integration events to RabbitMQ (a topic exchange, routing key = the
/// event type). Best-effort by design: the enrollment is already committed to the database, so
/// a broker outage is logged and swallowed rather than failing the operator's request — the
/// same posture the Redis cache uses. The connection is created lazily and shared; a
/// lightweight channel is opened per publish.
/// </summary>
public sealed partial class RabbitMqEventPublisher(string connectionString, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnection? _connection;

    public async Task PublishAsync(EnrollmentIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(
                RabbitMqTopology.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType(), Json);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = integrationEvent.EnrollmentId.ToString(),
                Type = integrationEvent.EventType,
            };

            await channel.BasicPublishAsync(
                exchange: RabbitMqTopology.Exchange,
                routingKey: integrationEvent.EventType,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            LogPublished(logger, integrationEvent.EventType, integrationEvent.ReferenceNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: never fail the operator's request because the broker is unavailable.
            LogPublishFailed(logger, integrationEvent.EventType, integrationEvent.ReferenceNumber, ex);
        }
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionGate.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {EventType} for enrollment {ReferenceNumber} to RabbitMQ.")]
    private static partial void LogPublished(ILogger logger, string eventType, string referenceNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish {EventType} for enrollment {ReferenceNumber} (best-effort; enrollment already saved).")]
    private static partial void LogPublishFailed(ILogger logger, string eventType, string referenceNumber, Exception exception);
}
