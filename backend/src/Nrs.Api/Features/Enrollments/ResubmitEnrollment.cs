using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the operator resubmits an application that a reviewer sent back for
/// corrections. The operator will have edited the biographic fields first (a NEEDS_CORRECTION
/// application is still editable); this resets it to SUBMITTED, clears the reviewer's note, and
/// asks the workflow to re-screen it — so it flows back through screening and, if still flagged,
/// the review queue. Delegated to the <see cref="IEnrollmentWorkflow"/>.
/// </summary>
public static class ResubmitEnrollment
{
    /// <summary>Outcome of a resubmit attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum Outcome
    {
        Resubmitted,
        NotFound,

        /// <summary>The application is not awaiting corrections, so it cannot be resubmitted (409).</summary>
        NotAwaitingCorrections,
    }

    public sealed class Handler(NrsDbContext db, IEnrollmentWorkflow workflow)
    {
        public async Task<(Outcome Outcome, EnrollmentDto? Enrollment)> HandleAsync(
            Guid id, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (enrollment is null)
            {
                return (Outcome.NotFound, null);
            }

            if (enrollment.Status != EnrollmentStatus.NEEDS_CORRECTION)
            {
                return (Outcome.NotAwaitingCorrections, enrollment.ToDto());
            }

            // Back to SUBMITTED so re-screening's queue-for-review step (guarded on SUBMITTED)
            // fires, and the reviewer's note is cleared now that it has been addressed. Reset the
            // escalation marker too: the resubmission starts a fresh review cycle whose new
            // boundary timer must be able to escalate again (the marker is the exactly-once guard).
            var now = DateTimeOffset.UtcNow;
            enrollment.Status = EnrollmentStatus.SUBMITTED;
            enrollment.CorrectionNote = null;
            enrollment.EscalatedAtUtc = null;
            enrollment.UpdatedAtUtc = now;
            db.Notifications.Add(DecisionNotifications.Resubmitted(enrollment, now));
            await db.SaveChangesAsync(cancellationToken);

            // Tell the parked process instance to loop back into screening (best-effort).
            await workflow.ResubmitAsync(enrollment, cancellationToken);

            return (Outcome.Resubmitted, enrollment.ToDto());
        }
    }
}
