using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the reviewer's work queue and its ownership actions (claim / release).
///
/// The queue is driven by the enrollment's own <see cref="EnrollmentStatus"/> — everything
/// PENDING_REVIEW (waiting, unassigned) or UNDER_REVIEW (claimed) — rather than by querying
/// Camunda's user-task index. The database is the source of truth for who owns what, which
/// keeps the screen accurate and lag-free (Camunda's task search is Elasticsearch-backed and
/// trails writes by a second or two) and sidesteps "ghost" tasks entirely. Camunda still owns
/// the flow: claiming/releasing mirror the assignment onto the Camunda user task best-effort
/// (so its Tasklist agrees), and the decision completes it.
/// </summary>
public static class ReviewTasks
{
    /// <summary>One reviewable item: its enrollment (carrying status + assignee), oldest-first.</summary>
    public record ReviewTaskDto
    {
        /// <summary>Reviewer who has claimed it, or null while it sits unassigned in the queue.</summary>
        public string? Assignee { get; init; }

        /// <summary>When it entered the queue / was last touched — used to sort oldest-first.</summary>
        public DateTimeOffset QueuedAtUtc { get; init; }

        public EnrollmentDto Enrollment { get; init; } = null!;
    }

    public sealed class ListHandler(NrsDbContext db)
    {
        public async Task<IReadOnlyList<ReviewTaskDto>> HandleAsync(CancellationToken cancellationToken)
        {
            // The whole live pipeline: waiting-to-be-claimed plus in-progress. The frontend
            // groups it into "available", "assigned to me" and "with others" by assignee.
            var open = await db.Enrollments.AsNoTracking()
                .Where(e => e.Status == EnrollmentStatus.PENDING_REVIEW
                            || e.Status == EnrollmentStatus.UNDER_REVIEW)
                .OrderBy(e => e.CreatedAtUtc) // FIFO: oldest applications first
                .Take(200)
                .ToListAsync(cancellationToken);

            return open
                .Select(e => new ReviewTaskDto
                {
                    Assignee = e.AssignedTo,
                    QueuedAtUtc = e.UpdatedAtUtc,
                    Enrollment = e.ToDto(),
                })
                .ToList();
        }
    }

    /// <summary>What happened to a claim attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum ClaimOutcome
    {
        /// <summary>The caller now owns the task (freshly claimed, or already theirs — idempotent).</summary>
        Claimed,

        /// <summary>Another reviewer already holds it, or it is no longer waiting (409).</summary>
        Taken,

        /// <summary>No enrollment exists with that id (404).</summary>
        NotFound,
    }

    /// <summary>
    /// Claims a pending review for the caller. The claim itself is a single conditional UPDATE
    /// (<c>WHERE status = PENDING_REVIEW AND assigned_to IS NULL</c>), so two reviewers racing
    /// for the same item cannot both win — the database, not a check-then-write, decides. The
    /// Camunda user-task assignment is then mirrored best-effort for Tasklist parity.
    /// </summary>
    public sealed class ClaimHandler(NrsDbContext db, IEnumerable<ICamundaClient> camunda)
    {
        private readonly ICamundaClient? _camunda = camunda.FirstOrDefault();

        public async Task<ClaimOutcome> HandleAsync(Guid enrollmentId, string reviewer, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);
            if (enrollment is null)
            {
                return ClaimOutcome.NotFound;
            }

            var now = DateTimeOffset.UtcNow;
            var claimed = await db.Enrollments
                .Where(e => e.Id == enrollmentId
                            && e.Status == EnrollmentStatus.PENDING_REVIEW
                            && e.AssignedTo == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, EnrollmentStatus.UNDER_REVIEW)
                    .SetProperty(e => e.AssignedTo, reviewer)
                    .SetProperty(e => e.AssignedAtUtc, now)
                    .SetProperty(e => e.UpdatedAtUtc, now), cancellationToken);

            if (claimed == 0)
            {
                // Lost the race, or it was never claimable. Re-reading tells us which: if the
                // caller already owns it, treat the (re-)claim as success (idempotent).
                var owner = await db.Enrollments.AsNoTracking()
                    .Where(e => e.Id == enrollmentId)
                    .Select(e => e.AssignedTo)
                    .FirstOrDefaultAsync(cancellationToken);
                return owner == reviewer ? ClaimOutcome.Claimed : ClaimOutcome.Taken;
            }

            await MirrorAssignmentAsync(enrollment.ProcessInstanceKey, reviewer, cancellationToken);
            return ClaimOutcome.Claimed;
        }

        private async Task MirrorAssignmentAsync(long? processInstanceKey, string reviewer, CancellationToken cancellationToken)
        {
            if (_camunda is null || processInstanceKey is not { } instanceKey)
            {
                return;
            }

            try
            {
                var tasks = await _camunda.SearchUserTasksAsync(
                    "CREATED", CamundaEnrollmentWorkflow.ProcessId, instanceKey, cancellationToken);
                var task = tasks.FirstOrDefault(t => t.ElementId == CamundaEnrollmentWorkflow.ReviewTaskElementId);
                if (task is not null)
                {
                    await _camunda.AssignUserTaskAsync(task.UserTaskKey, reviewer, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Best-effort Tasklist parity only — the database already owns the claim.
            }
        }
    }

    /// <summary>What happened to a release attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum ReleaseOutcome
    {
        /// <summary>Handed back to the queue (PENDING_REVIEW, unassigned).</summary>
        Released,

        /// <summary>The caller is not the assignee, or it is not under review (403).</summary>
        NotAssignee,

        /// <summary>No enrollment exists with that id (404).</summary>
        NotFound,
    }

    /// <summary>
    /// Releases a review the caller holds back into the shared queue. The inverse of a claim,
    /// and just as conditional: <c>WHERE assigned_to = :me AND status = UNDER_REVIEW</c>.
    /// </summary>
    public sealed class ReleaseHandler(NrsDbContext db, IEnumerable<ICamundaClient> camunda)
    {
        private readonly ICamundaClient? _camunda = camunda.FirstOrDefault();

        public async Task<ReleaseOutcome> HandleAsync(Guid enrollmentId, string reviewer, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);
            if (enrollment is null)
            {
                return ReleaseOutcome.NotFound;
            }

            var now = DateTimeOffset.UtcNow;
            var released = await db.Enrollments
                .Where(e => e.Id == enrollmentId
                            && e.Status == EnrollmentStatus.UNDER_REVIEW
                            && e.AssignedTo == reviewer)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, EnrollmentStatus.PENDING_REVIEW)
                    .SetProperty(e => e.AssignedTo, (string?)null)
                    .SetProperty(e => e.AssignedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(e => e.UpdatedAtUtc, now), cancellationToken);

            if (released == 0)
            {
                return ReleaseOutcome.NotAssignee;
            }

            await MirrorUnassignmentAsync(enrollment.ProcessInstanceKey, cancellationToken);
            return ReleaseOutcome.Released;
        }

        private async Task MirrorUnassignmentAsync(long? processInstanceKey, CancellationToken cancellationToken)
        {
            if (_camunda is null || processInstanceKey is not { } instanceKey)
            {
                return;
            }

            try
            {
                var tasks = await _camunda.SearchUserTasksAsync(
                    "CREATED", CamundaEnrollmentWorkflow.ProcessId, instanceKey, cancellationToken);
                var task = tasks.FirstOrDefault(t => t.ElementId == CamundaEnrollmentWorkflow.ReviewTaskElementId);
                if (task is not null)
                {
                    await _camunda.UnassignUserTaskAsync(task.UserTaskKey, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Best-effort Tasklist parity only — the database already owns the release.
            }
        }
    }
}
