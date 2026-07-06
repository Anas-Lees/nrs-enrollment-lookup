using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Fallback <see cref="IEnrollmentWorkflow"/> used when no Camunda engine is configured. There
/// is no orchestration to run, so submitting is a no-op (the RabbitMQ consumer, if present,
/// still advances SUBMITTED → UNDER_REVIEW) and a decision is applied straight to the database.
/// This keeps the approve/reject feature working in plain local dev and integration tests.
/// </summary>
public sealed class DbEnrollmentWorkflow(NrsDbContext db) : IEnrollmentWorkflow
{
    public Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<DecisionResult> DecideAsync(
        Guid enrollmentId, bool approved, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        // Someone else decided in the meantime — their decision stands, this one isn't recorded.
        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED)
        {
            return new DecisionResult(enrollment.ToDto(), Settled: true, Recorded: false);
        }

        enrollment.Status = approved ? EnrollmentStatus.APPROVED : EnrollmentStatus.REJECTED;
        enrollment.DecidedBy = decidedBy;
        enrollment.DecisionNotes = notes;
        enrollment.DecidedAtUtc = now;
        enrollment.UpdatedAtUtc = now;
        // Close the loop with the submitting operator, same as the Camunda worker does.
        db.Notifications.Add(DecisionNotifications.Decided(enrollment, now));
        await db.SaveChangesAsync(cancellationToken);
        // No engine: the write is synchronous, so the decision has always settled.
        return new DecisionResult(enrollment.ToDto(), Settled: true);
    }
}
