using Nrs.Domain.Entities;
using Nrs.Domain.Enums;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Builds the "your application was decided" notification for the submitting operator. One
/// place for the wording so the Camunda worker and the direct-write fallbacks stay identical.
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
}
