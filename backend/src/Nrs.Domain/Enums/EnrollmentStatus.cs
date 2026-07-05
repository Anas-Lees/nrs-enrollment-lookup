namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Lifecycle status of an enrollment application. Persisted as a string token.
/// The transition SUBMITTED -> UNDER_REVIEW is performed by the background consumer
/// after it receives the "enrollment submitted" message from RabbitMQ.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Being drafted; not yet submitted for processing.</summary>
    DRAFT,

    /// <summary>Submitted by the operator; queued for review.</summary>
    SUBMITTED,

    /// <summary>Picked up from the queue and under review.</summary>
    UNDER_REVIEW,

    /// <summary>Review complete; application approved.</summary>
    APPROVED,

    /// <summary>Review complete; application rejected.</summary>
    REJECTED
}

#pragma warning restore CA1707
