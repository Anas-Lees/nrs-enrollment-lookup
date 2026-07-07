using Nrs.Domain.Entities;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Completes the card-office user tasks (print, collect) on an enrollment's Camunda process
/// instance, advancing the fulfilment flow. Mirrors the review decision's robustness: it targets
/// the newest open task and re-searches when a completion loses to the Elasticsearch-lagged task
/// index. Returns false when there is no engine, the engine is unreachable, or no task could be
/// completed — the caller then applies the card-status change directly.
/// </summary>
public sealed class CardFulfilment(IEnumerable<ICamundaClient> camunda)
{
    public const string PrintTaskElementId = "Activity_PrintCard";
    public const string CollectTaskElementId = "Activity_Collect";

    private readonly ICamundaClient? _camunda = camunda.FirstOrDefault();

    /// <summary>True if the given user task was completed via Camunda (the workflow will advance).</summary>
    public async Task<bool> CompleteTaskAsync(Enrollment enrollment, string elementId, CancellationToken cancellationToken)
    {
        if (_camunda is null || enrollment.ProcessInstanceKey is not { } instanceKey)
        {
            return false;
        }

        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var tasks = await _camunda.SearchUserTasksAsync(
                    "CREATED", CamundaEnrollmentWorkflow.ProcessId, instanceKey, cancellationToken);
                var task = tasks
                    .Where(t => t.ElementId == elementId)
                    .OrderByDescending(t => t.CreationDate)
                    .FirstOrDefault();
                if (task is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                    continue;
                }

                if (await _camunda.CompleteUserTaskAsync(task.UserTaskKey, new { }, cancellationToken))
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken); // stale index entry — re-search
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Engine down: the caller degrades to a direct card-status write.
        }

        return false;
    }
}
