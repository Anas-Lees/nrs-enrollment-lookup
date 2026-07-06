using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// Background worker that consumes enrollment events from RabbitMQ. When an enrollment is
/// submitted it advances it from SUBMITTED to UNDER_REVIEW — the downstream "review pickup"
/// that decouples slow processing from the operator's fast web request. It connects with
/// retry, so it tolerates the broker not being ready the instant the API starts.
/// </summary>
public sealed partial class EnrollmentEventsConsumer(
    string connectionString,
    IServiceScopeFactory scopeFactory,
    ILogger<EnrollmentEventsConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);
        if (_channel is null)
        {
            return; // cancelled before we connected
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(
            RabbitMqTopology.ReviewQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        LogListening(logger, RabbitMqTopology.ReviewQueue);

        // Keep the worker alive until the host stops.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.RoutingKey == RabbitMqTopology.SubmittedRoutingKey)
            {
                var message = JsonSerializer.Deserialize<EnrollmentSubmitted>(eventArgs.Body.Span, Json);
                if (message is not null)
                {
                    await MoveToUnderReviewAsync(message.EnrollmentId, message.ReferenceNumber);
                }
            }
            else
            {
                LogObserved(logger, eventArgs.RoutingKey);
            }

            if (_channel is not null)
            {
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            LogHandleFailed(logger, eventArgs.RoutingKey, ex);
            if (_channel is not null)
            {
                // Don't requeue a poison message endlessly — drop it after logging.
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }

    private async Task MoveToUnderReviewAsync(Guid enrollmentId, string referenceNumber)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId);
        if (enrollment is null || enrollment.Status != EnrollmentStatus.SUBMITTED)
        {
            return; // already advanced, or gone
        }

        // No engine here to screen or auto-approve, so a submitted application simply joins the
        // shared review queue, unassigned, for a reviewer to claim (PENDING_REVIEW).
        enrollment.Status = EnrollmentStatus.PENDING_REVIEW;
        enrollment.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        LogUnderReview(logger, referenceNumber);
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _channel.ExchangeDeclareAsync(
                    RabbitMqTopology.Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(
                    RabbitMqTopology.ReviewQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(
                    RabbitMqTopology.ReviewQueue, RabbitMqTopology.Exchange, RabbitMqTopology.RoutingPattern, cancellationToken: stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogConnectRetry(logger, attempt, ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 3)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment review worker listening on {Queue}.")]
    private static partial void LogListening(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment {ReferenceNumber} moved to PENDING_REVIEW.")]
    private static partial void LogUnderReview(ILogger logger, string referenceNumber);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Observed enrollment event {RoutingKey} (no action).")]
    private static partial void LogObserved(ILogger logger, string routingKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to handle enrollment event {RoutingKey}; dropping.")]
    private static partial void LogHandleFailed(ILogger logger, string routingKey, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "RabbitMQ not ready (attempt {Attempt}): {Error}. Retrying…")]
    private static partial void LogConnectRetry(ILogger logger, int attempt, string error);
}
