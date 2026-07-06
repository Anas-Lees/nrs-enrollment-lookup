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
            NotificationJob => await SendNotificationAsync(db, enrollment, job, stoppingToken),
            _ => null,
        };

        await camunda.CompleteJobAsync(job.JobKey, outputVariables, stoppingToken);
    }

    /// <summary>Automated screening — the process routes on the returned variables.</summary>
    private async Task<object> ScreenAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        var result = await EnrollmentScreening.ScreenAsync(db, enrollment, ct);

        // Persist the flags so reviewers see why the application was routed to them.
        enrollment.ScreeningFlags = result.Flags.Count > 0 ? string.Join(",", result.Flags) : null;
        enrollment.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        LogScreened(logger, enrollment.ReferenceNumber, result.AutoApprove, enrollment.ScreeningFlags ?? "");

        return result.AutoApprove
            ? new
            {
                autoApprove = true,
                screeningFlags = result.Flags,
                decidedBy = "auto-screening",
                decisionNotes = "Clean renewal approved by automated screening.",
            }
            : new { autoApprove = false, screeningFlags = result.Flags };
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
