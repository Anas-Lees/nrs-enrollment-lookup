using Nrs.Domain.Entities;
using Nrs.Domain.Enums;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Builds the enrollment-lifecycle notifications for the submitting operator. One place for the
/// wording so the Camunda worker and the direct-write fallbacks stay identical.
/// </summary>
public static class DecisionNotifications
{
    public static Notification Decided(Enrollment enrollment, DateTimeOffset now)
    {
        var approved = enrollment.Status == EnrollmentStatus.APPROVED;
        var outcomeEn = approved ? "approved" : "rejected";
        var outcomeAr = approved ? "اعتُمد" : "رُفض";
        return new Notification
        {
            Id = Guid.NewGuid(),
            Recipient = enrollment.CreatedBy,
            Kind = "decided",
            EnrollmentId = enrollment.Id,
            ReferenceNumber = enrollment.ReferenceNumber,
            MessageEn = $"Enrollment {enrollment.ReferenceNumber} was {outcomeEn} by {enrollment.DecidedBy}.",
            MessageAr = $"{outcomeAr} التسجيل {enrollment.ReferenceNumber} بواسطة {enrollment.DecidedBy}.",
            CreatedAtUtc = now,
        };
    }

    /// <summary>Tells the submitting operator their application needs changes and must be resubmitted.</summary>
    public static Notification CorrectionsRequested(Enrollment enrollment, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Recipient = enrollment.CreatedBy,
        Kind = "corrections-requested",
        EnrollmentId = enrollment.Id,
        ReferenceNumber = enrollment.ReferenceNumber,
        MessageEn = $"Enrollment {enrollment.ReferenceNumber} needs corrections before it can proceed.",
        MessageAr = $"يحتاج التسجيل {enrollment.ReferenceNumber} إلى تصحيحات قبل المتابعة.",
        CreatedAtUtc = now,
    };

    /// <summary>Confirms to the operator that a corrected application was resubmitted for review.</summary>
    public static Notification Resubmitted(Enrollment enrollment, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Recipient = enrollment.CreatedBy,
        Kind = "resubmitted",
        EnrollmentId = enrollment.Id,
        ReferenceNumber = enrollment.ReferenceNumber,
        MessageEn = $"Enrollment {enrollment.ReferenceNumber} was resubmitted and is back in review.",
        MessageAr = $"أُعيد إرسال التسجيل {enrollment.ReferenceNumber} وعاد إلى المراجعة.",
        CreatedAtUtc = now,
    };

    /// <summary>Tells the operator an unactioned correction lapsed past its deadline and was closed.</summary>
    public static Notification Abandoned(Enrollment enrollment, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Recipient = enrollment.CreatedBy,
        Kind = "abandoned",
        EnrollmentId = enrollment.Id,
        ReferenceNumber = enrollment.ReferenceNumber,
        MessageEn = $"Enrollment {enrollment.ReferenceNumber} was closed — no corrections were received in time.",
        MessageAr = $"أُغلق التسجيل {enrollment.ReferenceNumber} — لم تُستلم التصحيحات في الوقت المحدد.",
        CreatedAtUtc = now,
    };

    /// <summary>Tells the operator the printed card is ready for the applicant to collect.</summary>
    public static Notification CardReadyForCollection(Enrollment enrollment, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Recipient = enrollment.CreatedBy,
        Kind = "card-ready",
        EnrollmentId = enrollment.Id,
        ReferenceNumber = enrollment.ReferenceNumber,
        MessageEn = $"The card for {enrollment.ReferenceNumber} is printed and ready for collection.",
        MessageAr = $"بطاقة التسجيل {enrollment.ReferenceNumber} طُبعت وجاهزة للاستلام.",
        CreatedAtUtc = now,
    };

    /// <summary>Confirms to the operator the card was collected and is now active.</summary>
    public static Notification CardIssued(Enrollment enrollment, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Recipient = enrollment.CreatedBy,
        Kind = "card-issued",
        EnrollmentId = enrollment.Id,
        ReferenceNumber = enrollment.ReferenceNumber,
        MessageEn = $"The card for {enrollment.ReferenceNumber} was collected and is now active.",
        MessageAr = $"استُلمت بطاقة التسجيل {enrollment.ReferenceNumber} وأصبحت نشطة.",
        CreatedAtUtc = now,
    };
}
