namespace Nrs.Api.Features.Enrollments.Messaging;

/// <summary>
/// The RabbitMQ names the enrollment publisher and consumer agree on. A topic exchange fans
/// enrollment events out by routing key; the review worker binds a durable queue to all of them.
/// </summary>
public static class RabbitMqTopology
{
    /// <summary>Topic exchange the enrollment events are published to.</summary>
    public const string Exchange = "nrs.enrollment";

    /// <summary>Durable queue the review worker consumes from.</summary>
    public const string ReviewQueue = "nrs.enrollment.review";

    /// <summary>Binding pattern — every enrollment.* event routes to the review queue.</summary>
    public const string RoutingPattern = "enrollment.#";

    /// <summary>Routing key / event type for a newly submitted enrollment.</summary>
    public const string SubmittedRoutingKey = "enrollment.submitted";

    /// <summary>Routing key / event type for an edited enrollment.</summary>
    public const string UpdatedRoutingKey = "enrollment.updated";

    /// <summary>Routing key / event type for an approved or rejected enrollment.</summary>
    public const string DecidedRoutingKey = "enrollment.decided";
}
