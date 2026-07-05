using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Drives the enrollment review through Camunda 8. Submitting starts a
/// <c>enrollment-review</c> process instance; deciding correlates the <c>enrollment-decision</c>
/// message the instance is waiting on. The actual status writes are done by
/// <see cref="EnrollmentProcessWorker"/> as it completes the process's service-task jobs, so
/// after correlating we briefly wait for that write to land before returning — giving the
/// operator a synchronous-feeling result for what is really an asynchronous engine.
/// </summary>
public sealed partial class CamundaEnrollmentWorkflow(
    ICamundaClient camunda,
    NrsDbContext db,
    ILogger<CamundaEnrollmentWorkflow> logger) : IEnrollmentWorkflow
{
    /// <summary>The BPMN process id (see enrollment-review.bpmn) and its decision message name.</summary>
    public const string ProcessId = "enrollment-review";
    public const string DecisionMessage = "enrollment-decision";

    public async Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        try
        {
            var instanceKey = await camunda.CreateProcessInstanceAsync(
                ProcessId,
                // enrollmentId is the message correlation key (see the BPMN subscription).
                new { enrollmentId = enrollment.Id.ToString(), referenceNumber = enrollment.ReferenceNumber },
                cancellationToken);
            LogStarted(logger, enrollment.ReferenceNumber, instanceKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort, like the event publisher: the enrollment is already saved, so a
            // Camunda blip must not fail the operator's request. It simply stays SUBMITTED.
            LogStartFailed(logger, enrollment.ReferenceNumber, ex);
        }
    }

    public async Task<DecisionResult> DecideAsync(Guid enrollmentId, bool approved, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        // Correlate the decision message. Just after the process starts there is a brief window
        // where the enrollment already reads UNDER_REVIEW but the message-catch subscription has
        // not opened yet (404). Retry within the deadline rather than failing the operator's click.
        var correlated = false;
        while (!correlated && DateTimeOffset.UtcNow < deadline)
        {
            correlated = await camunda.CorrelateMessageAsync(
                DecisionMessage, enrollmentId.ToString(), new { approved }, cancellationToken);
            if (!correlated)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
        }

        if (!correlated)
        {
            // The subscription never appeared in time — the worker/engine is unhealthy. Report the
            // decision as accepted-but-not-settled so the endpoint returns 202, not a false 200.
            LogDecisionPending(logger, enrollmentId);
            var current = await db.Enrollments.AsNoTracking()
                .FirstAsync(e => e.Id == enrollmentId, cancellationToken);
            return new DecisionResult(current.ToDto(), Settled: false);
        }

        // The worker applies APPROVED/REJECTED as it completes the follow-on job — normally
        // sub-second. Poll a fresh read briefly so the response reflects the settled status.
        Enrollment enrollment;
        do
        {
            enrollment = await db.Enrollments.AsNoTracking()
                .FirstAsync(e => e.Id == enrollmentId, cancellationToken);
            if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED)
            {
                return new DecisionResult(enrollment.ToDto(), Settled: true);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        // Correlated, but the status write has not landed yet: accepted, not settled. The queue
        // list will show the final status on its next refresh.
        LogDecisionPending(logger, enrollmentId);
        return new DecisionResult(enrollment.ToDto(), Settled: false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started Camunda review for enrollment {ReferenceNumber} (instance {InstanceKey}).")]
    private static partial void LogStarted(ILogger logger, string referenceNumber, long instanceKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start Camunda review for enrollment {ReferenceNumber} (best-effort; enrollment already saved).")]
    private static partial void LogStartFailed(ILogger logger, string referenceNumber, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Decision for enrollment {EnrollmentId} correlated but the status had not settled yet.")]
    private static partial void LogDecisionPending(ILogger logger, Guid enrollmentId);
}
