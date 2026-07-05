namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// Publishes enrollment integration events to the message broker. Implementations are
/// best-effort: a broker outage must never fail the web request that already committed
/// the enrollment to the database (mirrors the cache-aside posture used for Redis).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(EnrollmentIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
