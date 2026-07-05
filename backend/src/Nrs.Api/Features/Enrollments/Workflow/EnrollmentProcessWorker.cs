using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// The Camunda side of the enrollment review. On startup it deploys the BPMN (retrying until the
/// broker is up), then runs a job worker per service task: it long-polls Camunda for work,
/// applies the matching status change to the enrollment in Oracle, and completes the job so the
/// process advances. This is the classic external-task worker pattern — the engine owns the flow,
/// this app owns the side effects.
/// </summary>
public sealed partial class EnrollmentProcessWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EnrollmentProcessWorker> logger) : BackgroundService
{
    private const string ResourceName = "enrollment-review.bpmn";
    private const string WorkerName = "nrs-api";

    // BPMN zeebe:taskDefinition types -> the status each one applies.
    private const string MarkUnderReviewJob = "mark-under-review";
    private const string ApplyApprovedJob = "apply-approved";
    private const string ApplyRejectedJob = "apply-rejected";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DeployWithRetryAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        // One long-poll loop per job type, running concurrently for the life of the app.
        await Task.WhenAll(
            PollLoopAsync(MarkUnderReviewJob, EnrollmentStatus.UNDER_REVIEW, stoppingToken),
            PollLoopAsync(ApplyApprovedJob, EnrollmentStatus.APPROVED, stoppingToken),
            PollLoopAsync(ApplyRejectedJob, EnrollmentStatus.REJECTED, stoppingToken));
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

    private async Task PollLoopAsync(string jobType, EnrollmentStatus targetStatus, CancellationToken stoppingToken)
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
                    try
                    {
                        await HandleJobAsync(scope.ServiceProvider, camunda, job, targetStatus, stoppingToken);
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
        IServiceProvider services, ICamundaClient camunda, CamundaJob job, EnrollmentStatus targetStatus, CancellationToken stoppingToken)
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
        if (enrollment is not null && enrollment.Status != targetStatus)
        {
            enrollment.Status = targetStatus;
            enrollment.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            LogApplied(logger, enrollment.ReferenceNumber, targetStatus);
        }

        await camunda.CompleteJobAsync(job.JobKey, variables: null, stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deployed {Resource} to Camunda (process-definition key {Key}).")]
    private static partial void LogDeployed(ILogger logger, string resource, long key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camunda not ready to deploy yet (attempt {Attempt}): {Error}. Retrying…")]
    private static partial void LogDeployRetry(ILogger logger, int attempt, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrollment {ReferenceNumber} moved to {Status} by the workflow.")]
    private static partial void LogApplied(ILogger logger, string referenceNumber, EnrollmentStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobType} ({JobKey}) had no usable enrollmentId; completing to unblock the process.")]
    private static partial void LogMissingId(ILogger logger, string jobType, long jobKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling {JobType} jobs failed: {Error}. Backing off.")]
    private static partial void LogPollError(ILogger logger, string jobType, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling {JobType} job {JobKey} failed: {Error}. Leaving it to retry after its lock expires.")]
    private static partial void LogJobError(ILogger logger, string jobType, long jobKey, string error);
}
