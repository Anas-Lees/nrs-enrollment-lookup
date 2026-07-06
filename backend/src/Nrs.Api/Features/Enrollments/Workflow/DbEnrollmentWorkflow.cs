using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Fallback <see cref="IEnrollmentWorkflow"/> used when no Camunda engine is configured. There
/// is no orchestration to run, so submitting is a no-op (the RabbitMQ consumer, if present,
/// advances SUBMITTED → PENDING_REVIEW) and every transition is applied straight to the
/// database. This keeps the review feature working in plain local dev and integration tests.
/// </summary>
public sealed class DbEnrollmentWorkflow(NrsDbContext db) : IEnrollmentWorkflow
{
    public Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<DecisionResult> DecideAsync(
        Guid enrollmentId, ReviewOutcome outcome, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        // Someone else moved it on in the meantime — their action stands, this one isn't recorded.
        if (enrollment.Status is not (EnrollmentStatus.UNDER_REVIEW or EnrollmentStatus.PENDING_REVIEW))
        {
            return new DecisionResult(enrollment.ToDto(), Settled: true, Recorded: false);
        }

        switch (outcome)
        {
            case ReviewOutcome.Corrections:
                // Back to the operator; the reviewer no longer owns it.
                enrollment.Status = EnrollmentStatus.NEEDS_CORRECTION;
                enrollment.CorrectionNote = notes;
                enrollment.AssignedTo = null;
                enrollment.AssignedAtUtc = null;
                enrollment.UpdatedAtUtc = now;
                db.Notifications.Add(DecisionNotifications.CorrectionsRequested(enrollment, now));
                break;

            default:
                var approved = outcome == ReviewOutcome.Approved;
                enrollment.Status = approved ? EnrollmentStatus.APPROVED : EnrollmentStatus.REJECTED;
                enrollment.DecidedBy = decidedBy;
                enrollment.DecisionNotes = notes;
                enrollment.DecidedAtUtc = now;
                enrollment.UpdatedAtUtc = now;
                db.Notifications.Add(DecisionNotifications.Decided(enrollment, now));
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        // No engine: the write is synchronous, so the transition has always settled.
        return new DecisionResult(enrollment.ToDto(), Settled: true);
    }

    public Task ResubmitAsync(Enrollment enrollment, CancellationToken cancellationToken) =>
        // The caller already reset the row to SUBMITTED; with no engine there is nothing more to
        // do (a RabbitMQ consumer, if configured, will re-queue it for review).
        Task.CompletedTask;

    public async Task<DecisionResult> WithdrawAsync(
        Guid enrollmentId, string withdrawnBy, string? reason, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED or EnrollmentStatus.WITHDRAWN)
        {
            return new DecisionResult(enrollment.ToDto(), Settled: true, Recorded: false);
        }

        enrollment.Status = EnrollmentStatus.WITHDRAWN;
        enrollment.DecidedBy = withdrawnBy;
        enrollment.DecisionNotes = reason;
        enrollment.DecidedAtUtc = now;
        enrollment.AssignedTo = null;
        enrollment.AssignedAtUtc = null;
        enrollment.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return new DecisionResult(enrollment.ToDto(), Settled: true);
    }
}
