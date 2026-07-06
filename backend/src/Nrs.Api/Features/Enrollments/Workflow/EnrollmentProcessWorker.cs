using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// The Camunda side of the enrollment review. On startup it deploys the BPMN (retrying until
/// the broker is up), then runs a job worker per service task. Camunda owns the flow; these
/// handlers own the side effects — status writes to Oracle, and the staff notifications:
/// <list type="bullet">
///   <item><c>screen-application</c> — automated registry checks; decides auto-approve vs review.</item>
///   <item><c>queue-for-review</c> — SUBMITTED → PENDING_REVIEW (into the shared queue, unassigned).</item>
///   <item><c>apply-approved</c> / <c>apply-rejected</c> — final status + decision audit fields,
///   and a "decided" notification back to the submitting operator.</item>
///   <item><c>send-notification</c> — process-driven notices (review queued → reviewers,
///   SLA escalation → supervisors).</item>
/// </list>
/// </summary>
public sealed partial class EnrollmentProcessWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrollmentProcessWorker> logger) : BackgroundService
{
    private const string ResourceName = "enrollment-review.bpmn";
    private const string WorkerName = "nrs-api";

    // BPMN zeebe:taskDefinition types.
    private const string ScreenJob = "screen-application";
    private const string QueueForReviewJob = "queue-for-review";
    private const string ApplyApprovedJob = "apply-approved";
    private const string ApplyRejectedJob = "apply-rejected";
    private const string ApplyCorrectionsJob = "apply-corrections-requested";
    private const string ApplyAbandonedJob = "apply-abandoned";
    private const string ApplyWithdrawnJob = "apply-withdrawn";
    private const string NotificationJob = "send-notification";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DeployWithRetryAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        // One long-poll loop per job type, running concurrently for the life of the app.
        await Task.WhenAll(
            PollLoopAsync(ScreenJob, stoppingToken),
            PollLoopAsync(QueueForReviewJob, stoppingToken),
            PollLoopAsync(ApplyApprovedJob, stoppingToken),
            PollLoopAsync(ApplyRejectedJob, stoppingToken),
            PollLoopAsync(ApplyCorrectionsJob, stoppingToken),
            PollLoopAsync(ApplyAbandonedJob, stoppingToken),
            PollLoopAsync(ApplyWithdrawnJob, stoppingToken),
            PollLoopAsync(NotificationJob, stoppingToken));
    }

    private async Task DeployWithRetryAsync(CancellationToken stoppingToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Enrollments", "Workflow", ResourceName);

        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var camunda = scope.ServiceProvider.GetRequiredService<ICamundaClient>();

                if (await camunda.IsReadyAsync(stoppingToken))
                {
                    var content = await File.ReadAllBytesAsync(path, stoppingToken);
                    var key = await camunda.DeployResourceAsync(ResourceName, content, stoppingToken);
                    LogDeployed(logger, ResourceName, key);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDeployRetry(logger, attempt, ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 3)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PollLoopAsync(string jobType, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var camunda = scope.ServiceProvider.GetRequiredService<ICamundaClient>();

                var jobs = await camunda.ActivateJobsAsync(
                    jobType,
                    maxJobs: 10,
                    lockTimeout: TimeSpan.FromSeconds(60),
                    requestTimeout: TimeSpan.FromSeconds(10),
                    worker: WorkerName,
                    stoppingToken);

                foreach (var job in jobs)
                {
                    // Isolate each job: one failure (a transient DB error, a Camunda hiccup on
                    // completion) must not abandon the other already-activated jobs in the batch.
                    // The failed job simply stays locked until its timeout, then Camunda re-offers it.
                    // Each job gets its OWN DI scope (own DbContext): otherwise a failed job's
                    // tracked-but-uncommitted changes would ride along into the next job's save.
                    try
                    {
                        using var jobScope = scopeFactory.CreateScope();
                        await HandleJobAsync(jobScope.ServiceProvider, camunda, job, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogJobError(logger, jobType, job.JobKey, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogPollError(logger, jobType, ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task HandleJobAsync(
        IServiceProvider services, ICamundaClient camunda, CamundaJob job, CancellationToken stoppingToken)
    {
        var rawId = job.GetString("enrollmentId");
        if (!Guid.TryParse(rawId, out var enrollmentId))
        {
            // No enrollment to act on — complete it anyway so the process is not stuck.
            LogMissingId(logger, job.Type, job.JobKey);
            await camunda.CompleteJobAsync(job.JobKey, variables: null, stoppingToken);
            return;
        }

        var db = services.GetRequiredService<NrsDbContext>();
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, stoppingToken);
        if (enrollment is null)
        {
            LogMissingId(logger, job.Type, job.JobKey);
            await camunda.CompleteJobAsync(job.JobKey, variables: null, stoppingToken);
            return;
        }

        object? outputVariables = job.Type switch
        {
            ScreenJob => await ScreenAsync(db, enrollment, stoppingToken),
            QueueForReviewJob => await QueueForReviewAsync(db, enrollment, stoppingToken),
            ApplyApprovedJob => await ApplyDecisionAsync(db, enrollment, job, approved: true, stoppingToken),
            ApplyRejectedJob => await ApplyDecisionAsync(db, enrollment, job, approved: false, stoppingToken),
            ApplyCorrectionsJob => await ApplyCorrectionsRequestedAsync(db, enrollment, job, stoppingToken),
            ApplyAbandonedJob => await ApplyAbandonedAsync(db, enrollment, stoppingToken),
            ApplyWithdrawnJob => await ApplyWithdrawnAsync(db, enrollment, job, stoppingToken),
            NotificationJob => await SendNotificationAsync(db, enrollment, job, stoppingToken),
            _ => null,
        };

        await camunda.CompleteJobAsync(job.JobKey, outputVariables, stoppingToken);
    }

    /// <summary>Automated screening — the process routes on the returned variables.</summary>
    private async Task<object> ScreenAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        var result = await EnrollmentScreening.ScreenAsync(db, enrollment, ct);

        // Persist the flags + risk so reviewers see why the application was routed to them, and
        // so the queue can badge high-risk items even before the user task exists.
        enrollment.ScreeningFlags = result.Flags.Count > 0 ? string.Join(",", result.Flags) : null;
        enrollment.RiskLevel = result.RiskLevel;
        enrollment.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        LogScreened(logger, enrollment.ReferenceNumber, result.AutoApprove, enrollment.ScreeningFlags ?? "");

        // reviewGroup drives the user task's candidate group (=reviewGroup): HIGH risk → supervisor.
        return result.AutoApprove
            ? new
            {
                autoApprove = true,
                screeningFlags = result.Flags,
                riskLevel = result.RiskLevel,
                reviewGroup = result.ReviewGroup,
                decidedBy = "auto-screening",
                decisionNotes = "Clean renewal approved by automated screening.",
            }
            : new
            {
                autoApprove = false,
                screeningFlags = result.Flags,
                riskLevel = result.RiskLevel,
                reviewGroup = result.ReviewGroup,
            };
    }

    /// <summary>
    /// SUBMITTED → PENDING_REVIEW: the flagged application joins the shared queue, unassigned.
    /// A reviewer takes ownership later by claiming it (PENDING_REVIEW → UNDER_REVIEW).
    /// </summary>
    private async Task<object?> QueueForReviewAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        if (enrollment.Status == EnrollmentStatus.SUBMITTED)
        {
            enrollment.Status = EnrollmentStatus.PENDING_REVIEW;
            enrollment.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            LogApplied(logger, enrollment.ReferenceNumber, EnrollmentStatus.PENDING_REVIEW);
        }

        return null;
    }

    /// <summary>Final status + decision audit trail, and tell the submitting operator.</summary>
    private async Task<object?> ApplyDecisionAsync(
        NrsDbContext db, Enrollment enrollment, CamundaJob job, bool approved, CancellationToken ct)
    {
        var targetStatus = approved ? EnrollmentStatus.APPROVED : EnrollmentStatus.REJECTED;
        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED)
        {
            return null; // already decided (idempotent retry)
        }

        var now = DateTimeOffset.UtcNow;
        enrollment.Status = targetStatus;
        enrollment.DecidedBy = job.GetString("decidedBy") ?? "unknown";
        enrollment.DecisionNotes = job.GetString("decisionNotes");
        enrollment.DecidedAtUtc = now;
        enrollment.UpdatedAtUtc = now;

        // Close the loop with the counter operator who captured the application.
        db.Notifications.Add(DecisionNotifications.Decided(enrollment, now));

        await db.SaveChangesAsync(ct);
        LogApplied(logger, enrollment.ReferenceNumber, targetStatus);
        return null;
    }

    /// <summary>
    /// Reviewer sent it back for corrections: NEEDS_CORRECTION, note attached, ownership cleared,
    /// and the operator is told. The process then waits for a resubmission or the deadline.
    /// </summary>
    private async Task<object?> ApplyCorrectionsRequestedAsync(
        NrsDbContext db, Enrollment enrollment, CamundaJob job, CancellationToken ct)
    {
        // Idempotent: only act while it is still under review.
        if (enrollment.Status is not (EnrollmentStatus.UNDER_REVIEW or EnrollmentStatus.PENDING_REVIEW))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        enrollment.Status = EnrollmentStatus.NEEDS_CORRECTION;
        enrollment.CorrectionNote = job.GetString("decisionNotes");
        enrollment.AssignedTo = null;
        enrollment.AssignedAtUtc = null;
        enrollment.UpdatedAtUtc = now;
        db.Notifications.Add(DecisionNotifications.CorrectionsRequested(enrollment, now));
        await db.SaveChangesAsync(ct);
        LogApplied(logger, enrollment.ReferenceNumber, EnrollmentStatus.NEEDS_CORRECTION);
        return null;
    }

    /// <summary>
    /// The correction deadline lapsed with no resubmission: close the application. Guarded on
    /// NEEDS_CORRECTION so a race (operator resubmitted just as the timer fired) does not clobber
    /// a now-active application.
    /// </summary>
    private async Task<object?> ApplyAbandonedAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        if (enrollment.Status != EnrollmentStatus.NEEDS_CORRECTION)
        {
            return null; // resubmitted in time, or already concluded — nothing to abandon.
        }

        var now = DateTimeOffset.UtcNow;
        enrollment.Status = EnrollmentStatus.REJECTED;
        enrollment.DecidedBy = "system";
        enrollment.DecisionNotes = "Closed automatically: no corrections were received by the deadline.";
        enrollment.DecidedAtUtc = now;
        enrollment.UpdatedAtUtc = now;
        db.Notifications.Add(DecisionNotifications.Abandoned(enrollment, now));
        await db.SaveChangesAsync(ct);
        LogApplied(logger, enrollment.ReferenceNumber, EnrollmentStatus.REJECTED);
        return null;
    }

    /// <summary>The applicant withdrew: settle to WITHDRAWN and clear any ownership.</summary>
    private async Task<object?> ApplyWithdrawnAsync(
        NrsDbContext db, Enrollment enrollment, CamundaJob job, CancellationToken ct)
    {
        // Idempotent: never overwrite a concluded application.
        if (enrollment.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED or EnrollmentStatus.WITHDRAWN)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        enrollment.Status = EnrollmentStatus.WITHDRAWN;
        enrollment.DecidedBy = job.GetString("withdrawnBy") ?? "operator";
        enrollment.DecisionNotes = job.GetString("withdrawReason");
        enrollment.DecidedAtUtc = now;
        enrollment.AssignedTo = null;
        enrollment.AssignedAtUtc = null;
        enrollment.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        LogApplied(logger, enrollment.ReferenceNumber, EnrollmentStatus.WITHDRAWN);
        return null;
    }

    /// <summary>Process-driven notices; the BPMN task's "kind" header says which one.</summary>
    private async Task<object?> SendNotificationAsync(
        NrsDbContext db, Enrollment enrollment, CamundaJob job, CancellationToken ct)
    {
        var kind = job.GetHeader("kind") ?? "unknown";
        var now = DateTimeOffset.UtcNow;

        switch (kind)
        {
            case "review-queued":
                // Idempotent: Camunda delivers at-least-once, so a re-offered job (e.g. after
                // a completion hiccup) must not add a second copy to the reviewers' bell.
                var alreadyQueued = await db.Notifications.AsNoTracking()
                    .AnyAsync(n => n.EnrollmentId == enrollment.Id && n.Kind == kind, ct);
                if (!alreadyQueued)
                {
                    db.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        Recipient = "reviewer",
                        Kind = kind,
                        EnrollmentId = enrollment.Id,
                        ReferenceNumber = enrollment.ReferenceNumber,
                        MessageEn = $"Enrollment {enrollment.ReferenceNumber} is waiting for review.",
                        MessageAr = $"التسجيل {enrollment.ReferenceNumber} في انتظار المراجعة.",
                        CreatedAtUtc = now,
                    });
                }

                break;

            case "escalated":
                // Skip when the review already concluded (the decision landed between the timer
                // firing and this job running), and when this is a redelivered duplicate
                // (EscalatedAtUtc doubles as the exactly-once marker). An overdue review is
                // usually still PENDING_REVIEW (nobody claimed it), but a claimed-then-stalled
                // one (UNDER_REVIEW) is overdue too — escalate either.
                if (enrollment.Status is EnrollmentStatus.PENDING_REVIEW or EnrollmentStatus.UNDER_REVIEW
                    && enrollment.EscalatedAtUtc is null)
                {
                    enrollment.EscalatedAtUtc = now;
                    enrollment.UpdatedAtUtc = now;
                    db.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        Recipient = "supervisor",
                        Kind = kind,
                        EnrollmentId = enrollment.Id,
                        ReferenceNumber = enrollment.ReferenceNumber,
                        MessageEn = $"Review of {enrollment.ReferenceNumber} is overdue and needs attention.",
                        MessageAr = $"مراجعة التسجيل {enrollment.ReferenceNumber} متأخرة وتحتاج إلى تدخل.",
                        CreatedAtUtc = now,
                    });
                }

                break;

            default:
                LogUnknownNotificationKind(logger, kind, job.JobKey);
                break;
        }

        await db.SaveChangesAsync(ct);
        LogNotified(logger, kind, enrollment.ReferenceNumber);
        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deployed {Resource} to Camunda (process-definition key {Key}).")]
    private static partial void LogDeployed(ILogger logger, string resource, long key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camunda not ready to deploy yet (attempt {Attempt}): {Error}. Retrying…")]
    private static partial void LogDeployRetry(ILogger logger, int attempt, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment {ReferenceNumber} moved to {Status} by the workflow.")]
    private static partial void LogApplied(ILogger logger, string referenceNumber, EnrollmentStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment {ReferenceNumber} screened: autoApprove={AutoApprove}, flags=[{Flags}].")]
    private static partial void LogScreened(ILogger logger, string referenceNumber, bool autoApprove, string flags);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent {Kind} notification for enrollment {ReferenceNumber}.")]
    private static partial void LogNotified(ILogger logger, string kind, string referenceNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification job {JobKey} had unknown kind '{Kind}'; completing without action.")]
    private static partial void LogUnknownNotificationKind(ILogger logger, string kind, long jobKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobType} ({JobKey}) had no usable enrollmentId; completing to unblock the process.")]
    private static partial void LogMissingId(ILogger logger, string jobType, long jobKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling {JobType} jobs failed: {Error}. Backing off.")]
    private static partial void LogPollError(ILogger logger, string jobType, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling {JobType} job {JobKey} failed: {Error}. Leaving it to retry after its lock expires.")]
    private static partial void LogJobError(ILogger logger, string jobType, long jobKey, string error);
}
