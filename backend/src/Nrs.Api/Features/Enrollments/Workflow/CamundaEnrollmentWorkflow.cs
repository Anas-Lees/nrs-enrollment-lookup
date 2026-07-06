using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Drives the enrollment review through Camunda 8. Submitting starts an
/// <c>enrollment-review</c> process instance (whose key is remembered on the enrollment row);
/// deciding completes the reviewer <em>user task</em> the instance is waiting on; resubmitting
/// and withdrawing correlate BPMN messages. The actual status writes are done by
/// <see cref="EnrollmentProcessWorker"/> as it completes the process's service-task jobs, so
/// after acting we briefly wait for that write to land before returning — giving the reviewer a
/// synchronous-feeling result for what is really an asynchronous engine.
/// </summary>
public sealed partial class CamundaEnrollmentWorkflow(
    ICamundaClient camunda,
    NrsDbContext db,
    IConfiguration configuration,
    ILogger<CamundaEnrollmentWorkflow> logger) : IEnrollmentWorkflow
{
    /// <summary>The BPMN process id and the reviewer user-task element (see enrollment-review.bpmn).</summary>
    public const string ProcessId = "enrollment-review";
    public const string ReviewTaskElementId = "Activity_Review";

    /// <summary>BPMN message names — correlated by the enrollment id (see the zeebe:subscription keys).</summary>
    public const string CorrectionsSubmittedMessage = "corrections-submitted";
    public const string WithdrawnMessage = "enrollment-withdrawn";

    private static readonly EnrollmentStatus[] DecideSettledStatuses =
        [EnrollmentStatus.APPROVED, EnrollmentStatus.REJECTED, EnrollmentStatus.NEEDS_CORRECTION];

    public async Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        try
        {
            // The SLA durations ride on the instance so the BPMN timers can read them.
            // Production-shaped defaults; the demo compose sets short values.
            var escalationAfter = configuration["Camunda:ReviewEscalationAfter"] ?? "PT48H";
            var correctionDeadline = configuration["Camunda:CorrectionDeadline"] ?? "P7D";

            var instanceKey = await camunda.CreateProcessInstanceAsync(
                ProcessId,
                new
                {
                    enrollmentId = enrollment.Id.ToString(),
                    referenceNumber = enrollment.ReferenceNumber,
                    submittedBy = enrollment.CreatedBy,
                    escalationAfter,
                    correctionDeadline,
                },
                cancellationToken);

            // Remember the instance so the decision endpoint can find the review user task
            // without guessing (searching by variables is slower and lag-prone).
            enrollment.ProcessInstanceKey = instanceKey;
            await db.SaveChangesAsync(cancellationToken);
            LogStarted(logger, enrollment.ReferenceNumber, instanceKey);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort, like the event publisher: the enrollment is already saved, so a
            // Camunda blip must not fail the operator's request. It simply stays SUBMITTED.
            LogStartFailed(logger, enrollment.ReferenceNumber, ex);
        }
    }

    public async Task<DecisionResult> DecideAsync(
        Guid enrollmentId, ReviewOutcome outcome, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        // Complete the open reviewer task on this enrollment's process instance. The task search
        // is Elasticsearch-backed, so it lags: a just-created task can be missing, and — after a
        // corrections loop-back — the previous, already-completed task can briefly still show as
        // CREATED. So target the NEWEST open review task, and if a completion loses (that task was
        // stale/already done) re-search for a fresher one rather than giving up. If Camunda is
        // unreachable, or no task ever appears (orphaned), fall through to the direct write.
        var camundaHealthy = true;
        var completed = false;
        if (enrollment.ProcessInstanceKey is { } instanceKey)
        {
            try
            {
                var searchDeadline = DateTimeOffset.UtcNow.AddSeconds(4);
                while (!completed && DateTimeOffset.UtcNow < searchDeadline)
                {
                    var tasks = await camunda.SearchUserTasksAsync(
                        "CREATED", ProcessId, instanceKey, cancellationToken);
                    var reviewTask = tasks
                        .Where(t => t.ElementId == ReviewTaskElementId)
                        .OrderByDescending(t => t.CreationDate) // freshest task wins over a stale index entry
                        .FirstOrDefault();
                    if (reviewTask is null)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                        continue;
                    }

                    completed = await camunda.CompleteUserTaskAsync(
                        reviewTask.UserTaskKey,
                        new { outcome = OutcomeToken(outcome), decidedBy, decisionNotes = notes },
                        cancellationToken);
                    if (!completed)
                    {
                        // The task we targeted was already done (a stale index entry or a genuine
                        // race). Re-search for a fresher one.
                        LogLostRace(logger, enrollment.ReferenceNumber);
                        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                camundaHealthy = false;
                LogEngineUnavailable(logger, enrollment.ReferenceNumber, ex.Message);
            }
        }

        if (completed)
        {
            return await PollForSettleAsync(enrollmentId, deadline, DecideSettledStatuses, cancellationToken);
        }

        // Not completed via Camunda (engine down, orphaned, or the task never settled to us). Apply
        // directly: the conditional UPDATE inside enforces UNDER_REVIEW + assignee, so if the
        // application actually concluded or moved on elsewhere it returns Recorded=false (409),
        // and if it is genuinely orphaned-but-still-ours it records the decision honestly.
        if (camundaHealthy)
        {
            LogOrphanedDecision(logger, enrollmentId);
        }

        return await ApplyDecisionDirectlyAsync(enrollment, outcome, decidedBy, notes, cancellationToken);
    }

    public async Task ResubmitAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        // The endpoint already reset the row to SUBMITTED. Tell the parked instance to loop back
        // into screening. The catch event only subscribes once the engine has completed the
        // request-corrections job and advanced the token to the event-based gateway — which can
        // trail the NEEDS_CORRECTION write by a few seconds under load — and correlate does not
        // buffer, so retry generously until the subscription is there.
        var correlationKey = enrollment.Id.ToString();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
        try
        {
            while (true)
            {
                var correlated = await camunda.CorrelateMessageAsync(
                    CorrectionsSubmittedMessage, correlationKey, new { }, cancellationToken);
                if (correlated || DateTimeOffset.UtcNow >= deadline)
                {
                    if (!correlated)
                    {
                        LogResubmitNotCorrelated(logger, enrollment.ReferenceNumber);
                    }

                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Engine down: the row is already SUBMITTED, so the application is not lost; it just
            // will not be re-screened until the engine is back. Best-effort, like submit.
            LogEngineUnavailable(logger, enrollment.ReferenceNumber, ex.Message);
        }
    }

    public async Task<DecisionResult> WithdrawAsync(
        Guid enrollmentId, string withdrawnBy, string? reason, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        // Already concluded — nothing to withdraw; report honestly.
        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED or EnrollmentStatus.WITHDRAWN)
        {
            return new DecisionResult(enrollment.ToDto(), Settled: true, Recorded: false);
        }

        var correlated = false;
        try
        {
            correlated = await camunda.CorrelateMessageAsync(
                WithdrawnMessage,
                enrollmentId.ToString(),
                new { withdrawnBy, withdrawReason = reason },
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            LogEngineUnavailable(logger, enrollment.ReferenceNumber, ex.Message);
        }

        if (!correlated)
        {
            // No subscription (instance already gone) or engine down: apply WITHDRAWN directly.
            return await ApplyWithdrawDirectlyAsync(enrollment, withdrawnBy, reason, cancellationToken);
        }

        return await PollForSettleAsync(
            enrollmentId, deadline, [EnrollmentStatus.WITHDRAWN], cancellationToken);
    }

    private static string OutcomeToken(ReviewOutcome outcome) => outcome switch
    {
        ReviewOutcome.Approved => "approved",
        ReviewOutcome.Corrections => "corrections",
        _ => "rejected",
    };

    /// <summary>
    /// The worker applies the settled status as it completes the follow-on job — normally
    /// sub-second. Poll a fresh read briefly so the response reflects it.
    /// </summary>
    private async Task<DecisionResult> PollForSettleAsync(
        Guid enrollmentId, DateTimeOffset deadline, EnrollmentStatus[] settledStatuses, CancellationToken cancellationToken)
    {
        Enrollment current;
        do
        {
            current = await db.Enrollments.AsNoTracking()
                .FirstAsync(e => e.Id == enrollmentId, cancellationToken);
            if (settledStatuses.Contains(current.Status))
            {
                return new DecisionResult(current.ToDto(), Settled: true);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        // Accepted, but the status write has not landed yet. The queue catches up on refresh.
        LogDecisionPending(logger, enrollmentId);
        return new DecisionResult(current.ToDto(), Settled: false);
    }

    private async Task<DecisionResult> ApplyDecisionDirectlyAsync(
        Enrollment enrollment, ReviewOutcome outcome, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        // The decision is applied as a single conditional UPDATE so the database — not this
        // stale in-memory read — arbitrates. The WHERE clause encodes the invariant the reviewer
        // relies on: it must still be UNDER_REVIEW and still assigned to this reviewer. If a rival
        // action (a withdrawal, the abandon timer, a release) concluded or moved it in the window
        // while we were degrading, zero rows change and the decision is honestly NOT recorded.
        var now = DateTimeOffset.UtcNow;
        var mine = db.Enrollments.Where(e =>
            e.Id == enrollment.Id
            && e.Status == EnrollmentStatus.UNDER_REVIEW
            && e.AssignedTo == decidedBy);

        int rows;
        if (outcome == ReviewOutcome.Corrections)
        {
            rows = await mine.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EnrollmentStatus.NEEDS_CORRECTION)
                .SetProperty(e => e.CorrectionNote, notes)
                .SetProperty(e => e.AssignedTo, (string?)null)
                .SetProperty(e => e.AssignedAtUtc, (DateTimeOffset?)null)
                .SetProperty(e => e.UpdatedAtUtc, now), cancellationToken);
        }
        else
        {
            var target = outcome == ReviewOutcome.Approved
                ? EnrollmentStatus.APPROVED
                : EnrollmentStatus.REJECTED;
            rows = await mine.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, target)
                .SetProperty(e => e.DecidedBy, decidedBy)
                .SetProperty(e => e.DecisionNotes, notes)
                .SetProperty(e => e.DecidedAtUtc, now)
                .SetProperty(e => e.UpdatedAtUtc, now), cancellationToken);
        }

        // Re-read fresh (ExecuteUpdate bypasses the tracked entity and the change tracker).
        var fresh = await db.Enrollments.AsNoTracking()
            .FirstAsync(e => e.Id == enrollment.Id, cancellationToken);
        if (rows == 0)
        {
            return new DecisionResult(fresh.ToDto(), Settled: true, Recorded: false);
        }

        db.Notifications.Add(outcome == ReviewOutcome.Corrections
            ? DecisionNotifications.CorrectionsRequested(fresh, now)
            : DecisionNotifications.Decided(fresh, now));
        await db.SaveChangesAsync(cancellationToken);
        await BestEffortCancelInstanceAsync(fresh, cancellationToken);
        return new DecisionResult(fresh.ToDto(), Settled: true);
    }

    private async Task<DecisionResult> ApplyWithdrawDirectlyAsync(
        Enrollment enrollment, string withdrawnBy, string? reason, CancellationToken cancellationToken)
    {
        // Conditional UPDATE: withdraw only wins if the application is still not concluded, so a
        // decision that landed in the degrade window is never clobbered (zero rows → not recorded).
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Enrollments
            .Where(e => e.Id == enrollment.Id
                        && e.Status != EnrollmentStatus.APPROVED
                        && e.Status != EnrollmentStatus.REJECTED
                        && e.Status != EnrollmentStatus.WITHDRAWN)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EnrollmentStatus.WITHDRAWN)
                .SetProperty(e => e.DecidedBy, withdrawnBy)
                .SetProperty(e => e.DecisionNotes, reason)
                .SetProperty(e => e.DecidedAtUtc, now)
                .SetProperty(e => e.AssignedTo, (string?)null)
                .SetProperty(e => e.AssignedAtUtc, (DateTimeOffset?)null)
                .SetProperty(e => e.UpdatedAtUtc, now), cancellationToken);

        var fresh = await db.Enrollments.AsNoTracking()
            .FirstAsync(e => e.Id == enrollment.Id, cancellationToken);
        if (rows == 0)
        {
            return new DecisionResult(fresh.ToDto(), Settled: true, Recorded: false);
        }

        await BestEffortCancelInstanceAsync(fresh, cancellationToken);
        return new DecisionResult(fresh.ToDto(), Settled: true);
    }

    /// <summary>
    /// We settled outside the engine, so the process instance is stranded. Best-effort cancel it
    /// (if the engine is reachable) so it does not linger or later fire a timer on a settled row.
    /// </summary>
    private async Task BestEffortCancelInstanceAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        if (enrollment.ProcessInstanceKey is not { } instanceKey)
        {
            return;
        }

        try
        {
            await camunda.CancelProcessInstanceAsync(instanceKey, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Engine still down or the instance already gone — the review list filters settled
            // enrollments out regardless, so a leftover instance is harmless.
            LogCancelFailed(logger, enrollment.ReferenceNumber, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started Camunda review for enrollment {ReferenceNumber} (instance {InstanceKey}).")]
    private static partial void LogStarted(ILogger logger, string referenceNumber, long instanceKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start Camunda review for enrollment {ReferenceNumber} (best-effort; enrollment already saved).")]
    private static partial void LogStartFailed(ILogger logger, string referenceNumber, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decision for enrollment {EnrollmentId} was recorded but the status had not settled yet.")]
    private static partial void LogDecisionPending(ILogger logger, Guid enrollmentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Camunda review task was waiting for enrollment {EnrollmentId} (orphaned); applied the decision directly to the database.")]
    private static partial void LogOrphanedDecision(ILogger logger, Guid enrollmentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Review task for enrollment {ReferenceNumber} was completed by someone else first.")]
    private static partial void LogLostRace(ILogger logger, string referenceNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camunda unreachable while acting on enrollment {ReferenceNumber} ({Error}); applying the change directly.")]
    private static partial void LogEngineUnavailable(ILogger logger, string referenceNumber, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resubmit of enrollment {ReferenceNumber} did not correlate a corrections-submitted message (no subscription); it stays SUBMITTED.")]
    private static partial void LogResubmitNotCorrelated(ILogger logger, string referenceNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not cancel the stranded process instance for enrollment {ReferenceNumber} ({Error}); it will be ignored by the review list.")]
    private static partial void LogCancelFailed(ILogger logger, string referenceNumber, string error);
}
