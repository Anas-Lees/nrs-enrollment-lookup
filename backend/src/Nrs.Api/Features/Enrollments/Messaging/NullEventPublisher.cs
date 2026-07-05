using Microsoft.Extensions.Logging;

namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// No-op publisher used when no broker is configured (local SQLite dev, tests). It keeps
/// the create/edit handlers identical whether or not RabbitMQ is present — the event is
/// simply dropped (logged at debug), and the enrollment still persists normally.
/// </summary>
public sealed partial class NullEventPublisher(ILogger<NullEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync(EnrollmentIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        LogDropped(logger, integrationEvent.EventType, integrationEvent.ReferenceNumber);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No message broker configured; dropping {EventType} for enrollment {ReferenceNumber}.")]
    private static partial void LogDropped(ILogger logger, string eventType, string referenceNumber);
}
