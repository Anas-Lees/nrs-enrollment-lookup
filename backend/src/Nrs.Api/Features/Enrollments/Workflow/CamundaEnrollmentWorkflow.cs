using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Drives the enrollment review through Camunda 8. Submitting starts an
/// <c>enrollment-review</c> process instance (whose key is remembered on the enrollment row);
/// deciding completes the reviewer <em>user task</em> the instance is waiting on. The actual
/// status writes are done by <see cref="EnrollmentProcessWorker"/> as it completes the
/// process's service-task jobs, so after completing the task we briefly wait for that write
/// to land before returning — giving the reviewer a synchronous-feeling result for what is
/// really an asynchronous engine.
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

    public async Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        try
        {
            // The escalation SLA rides on the instance so the BPMN timer can read it.
            // Production-shaped default; the demo compose sets a couple of minutes.
            var escalationAfter = configuration["Camunda:ReviewEscalationAfter"] ?? "PT48H";

            var instanceKey = await camunda.CreateProcessInstanceAsync(
                ProcessId,
                new
                {
                    enrollmentId = enrollment.Id.ToString(),
                    referenceNumber = enrollment.ReferenceNumber,
                    submittedBy = enrollment.CreatedBy,
                    escalationAfter,
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
            // Note the filter: an HttpClient timeout throws TaskCanceledException (an
            // OperationCanceledException), so filtering on the exception TYPE would let an
            // engine hang escape and 500 the create — only real caller cancellation may.
            LogStartFailed(logger, enrollment.ReferenceNumber, ex);
        }
    }

    public async Task<DecisionResult> DecideAsync(
        Guid enrollmentId, bool approved, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, cancellationToken);

        // Find the open reviewer task on this enrollment's process instance. The search is
        // Elasticsearch-backed, so a just-created task can lag a moment — retry briefly. If
        // Camunda itself is unreachable, the reviewer's decision must not be lost: degrade to
        // the direct write, same as the orphan path.
        CamundaUserTask? reviewTask = null;
        var camundaHealthy = true;
        if (enrollment.ProcessInstanceKey is { } instanceKey)
        {
            try
            {
                var searchDeadline = DateTimeOffset.UtcNow.AddSeconds(3);
                while (reviewTask is null && DateTimeOffset.UtcNow < searchDeadline)
                {
                    var tasks = await camunda.SearchUserTasksAsync(
                        "CREATED", ProcessId, instanceKey, cancellationToken);
                    reviewTask = tasks.FirstOrDefault(t => t.ElementId == ReviewTaskElementId);
                    if (reviewTask is null)
                    {
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

        if (reviewTask is null)
        {
            // No task is waiting: the enrollment reached UNDER_REVIEW outside Camunda, its
            // instance was lost to an engine restart, or the engine is down right now. The
            // reviewer's decision still counts, so apply it directly.
            if (camundaHealthy)
            {
                LogOrphanedDecision(logger, enrollmentId);
            }

            return await ApplyDirectlyAsync(enrollment, approved, decidedBy, notes, cancellationToken);
        }

        bool completed;
        try
        {
            completed = await camunda.CompleteUserTaskAsync(
                reviewTask.UserTaskKey,
                new { approved, decidedBy, decisionNotes = notes },
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            LogEngineUnavailable(logger, enrollment.ReferenceNumber, ex.Message);
            return await ApplyDirectlyAsync(enrollment, approved, decidedBy, notes, cancellationToken);
        }

        if (!completed)
        {
            // Another reviewer finished the task between our search and completion. THEIR
            // decision stands; this one was never recorded — report the conflict honestly
            // (after a short poll so the response carries the winner's settled outcome).
            LogLostRace(logger, enrollment.ReferenceNumber);
            var losing = await PollForSettleAsync(enrollmentId, deadline, cancellationToken);
            return losing with { Recorded = false };
        }

        return await PollForSettleAsync(enrollmentId, deadline, cancellationToken);
    }

    /// <summary>
    /// The worker applies APPROVED/REJECTED as it completes the follow-on job — normally
    /// sub-second. Poll a fresh read briefly so the response reflects the settled status.
    /// </summary>
    private async Task<DecisionResult> PollForSettleAsync(
        Guid enrollmentId, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        Enrollment current;
        do
        {
            current = await db.Enrollments.AsNoTracking()
                .FirstAsync(e => e.Id == enrollmentId, cancellationToken);
            if (current.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED)
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

    private async Task<DecisionResult> ApplyDirectlyAsync(
        Enrollment enrollment, bool approved, string decidedBy, string? notes, CancellationToken cancellationToken)
    {
        // Someone else settled it while we were degrading — their decision stands.
        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED)
        {
            return new DecisionResult(enrollment.ToDto(), Settled: true, Recorded: false);
        }

        var now = DateTimeOffset.UtcNow;
        enrollment.Status = approved ? EnrollmentStatus.APPROVED : EnrollmentStatus.REJECTED;
        enrollment.DecidedBy = decidedBy;
        enrollment.DecisionNotes = notes;
        enrollment.DecidedAtUtc = now;
        enrollment.UpdatedAtUtc = now;
        // The Camunda worker normally tells the submitting operator; on the direct path we do.
        db.Notifications.Add(DecisionNotifications.Decided(enrollment, now));
        await db.SaveChangesAsync(cancellationToken);

        // We decided outside the engine, so the process instance is now stranded at its user
        // task. Best-effort cancel it (if the engine is reachable) so it does not linger as a
        // ghost review task or later fire its escalation timer on an already-decided enrollment.
        if (enrollment.ProcessInstanceKey is { } instanceKey)
        {
            try
            {
                await camunda.CancelProcessInstanceAsync(instanceKey, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Engine still down or the instance already gone — the review list filters
                // decided enrollments out regardless, so a leftover instance is harmless.
                LogCancelFailed(logger, enrollment.ReferenceNumber, ex.Message);
            }
        }

        return new DecisionResult(enrollment.ToDto(), Settled: true);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camunda unreachable while deciding enrollment {ReferenceNumber} ({Error}); applying the decision directly.")]
    private static partial void LogEngineUnavailable(ILogger logger, string referenceNumber, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not cancel the stranded process instance for enrollment {ReferenceNumber} ({Error}); it will be ignored by the review list.")]
    private static partial void LogCancelFailed(ILogger logger, string referenceNumber, string error);
}
