namespace Nrs.Domain.Entities;

/// <summary>
/// An in-app notification for staff, produced by the enrollment review workflow: reviewers
/// are told when an application queues for review, the submitting operator when it is
/// decided, and supervisors when a review breaches its SLA. Maps to the NOTIFICATION table.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    /// <summary>
    /// Who should see it: a username (e.g. "operator1") or a role name (e.g. "reviewer",
    /// "supervisor") — role notifications appear for every member of that role.
    /// </summary>
    public string Recipient { get; set; } = null!;

    /// <summary>Discriminator for icon/wording, e.g. "review-queued", "decided", "escalated".</summary>
    public string Kind { get; set; } = null!;

    /// <summary>The enrollment this is about, when applicable.</summary>
    public Guid? EnrollmentId { get; set; }

    /// <summary>Human-friendly reference of that enrollment, denormalised for display.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>English message body.</summary>
    public string MessageEn { get; set; } = null!;

    /// <summary>Arabic message body (the SPA is bilingual).</summary>
    public string MessageAr { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Null while unread; set when the user opens/dismisses it.</summary>
    public DateTimeOffset? ReadAtUtc { get; set; }
}
