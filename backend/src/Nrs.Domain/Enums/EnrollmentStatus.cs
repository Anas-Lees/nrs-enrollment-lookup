namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Lifecycle status of an enrollment application. Persisted as a string token.
/// Automated screening moves a submitted application either straight to APPROVED (a clean
/// renewal) or to PENDING_REVIEW (flagged, waiting in the shared queue). A reviewer then
/// <em>claims</em> it — PENDING_REVIEW -> UNDER_REVIEW, stamping the assignee — and only that
/// assignee can approve or reject it, or release it back to PENDING_REVIEW.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Being drafted; not yet submitted for processing.</summary>
    DRAFT,

    /// <summary>Submitted by the operator; automated screening has not routed it yet.</summary>
    SUBMITTED,

    /// <summary>Flagged by screening and sitting in the shared review queue, unassigned.</summary>
    PENDING_REVIEW,

    /// <summary>Claimed by a reviewer and actively under review (see <c>Enrollment.AssignedTo</c>).</summary>
    UNDER_REVIEW,

    /// <summary>Review complete; application approved.</summary>
    APPROVED,

    /// <summary>Review complete; application rejected.</summary>
    REJECTED
}

#pragma warning restore CA1707
