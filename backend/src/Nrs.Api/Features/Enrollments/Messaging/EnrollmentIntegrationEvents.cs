namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// Base for the small messages the enrollment feature publishes to the broker when
/// something happens. <see cref="EventType"/> doubles as the RabbitMQ routing key.
/// </summary>
public abstract record EnrollmentIntegrationEvent
{
    /// <summary>Routing key / discriminator, e.g. "enrollment.submitted".</summary>
    public abstract string EventType { get; }

    public Guid EnrollmentId { get; init; }

    public string ReferenceNumber { get; init; } = null!;

    /// <summary>Operator who triggered the event.</summary>
    public string Operator { get; init; } = null!;

    public DateTimeOffset OccurredAtUtc { get; init; }
}

/// <summary>Raised when an operator submits a new enrollment application.</summary>
public record EnrollmentSubmitted : EnrollmentIntegrationEvent
{
    public override string EventType => RabbitMqTopology.SubmittedRoutingKey;
}

/// <summary>Raised when an existing enrollment application is edited.</summary>
public record EnrollmentUpdated : EnrollmentIntegrationEvent
{
    public override string EventType => RabbitMqTopology.UpdatedRoutingKey;
}
