using System.Text.Json;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Thin client over the Camunda 8 REST API (<c>/v2</c>). Only the handful of operations the
/// enrollment review needs are exposed: deploy the process, start an instance, pull and
/// complete jobs, and correlate the decision message. gRPC (port 26500) is deliberately
/// unused — the REST API is the supported surface from 8.8 onwards.
/// </summary>
public interface ICamundaClient
{
    /// <summary>True once the broker answers <c>GET /v2/topology</c> — used to gate the first deploy.</summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);

    /// <summary>Deploys a single BPMN resource; returns the created process-definition key.</summary>
    Task<long> DeployResourceAsync(string resourceName, byte[] content, CancellationToken cancellationToken);

    /// <summary>Starts a process instance by its BPMN id; returns the process-instance key.</summary>
    Task<long> CreateProcessInstanceAsync(string processDefinitionId, object variables, CancellationToken cancellationToken);

    /// <summary>Long-polls for jobs of one type (service-task worker pull model).</summary>
    Task<IReadOnlyList<CamundaJob>> ActivateJobsAsync(
        string jobType, int maxJobs, TimeSpan lockTimeout, TimeSpan requestTimeout, string worker, CancellationToken cancellationToken);

    /// <summary>Marks a job done so the process advances past its service task.</summary>
    Task CompleteJobAsync(long jobKey, object? variables, CancellationToken cancellationToken);

    /// <summary>
    /// Correlates a message to a waiting instance (by correlation key). Returns false when no
    /// instance is subscribed yet (HTTP 404) so the caller can retry; throws on any other error.
    /// </summary>
    Task<bool> CorrelateMessageAsync(string name, string correlationKey, object variables, CancellationToken cancellationToken);

    /// <summary>
    /// Searches user tasks (Elasticsearch-backed — freshly created tasks can lag a second or
    /// two). Filters are optional; state is e.g. "CREATED" for open tasks.
    /// </summary>
    Task<IReadOnlyList<CamundaUserTask>> SearchUserTasksAsync(
        string state, string? processDefinitionId, long? processInstanceKey, CancellationToken cancellationToken);

    /// <summary>
    /// Claims a user task for the given assignee. Returns false when the task is already
    /// claimed by someone else or no longer exists (HTTP 409/404); throws on any other error.
    /// </summary>
    Task<bool> AssignUserTaskAsync(long userTaskKey, string assignee, CancellationToken cancellationToken);

    /// <summary>
    /// Completes a user task with the given variables, advancing the process. Returns false
    /// when the task no longer exists / is not open (HTTP 404 or 409 — someone else finished
    /// it first); throws on any other error.
    /// </summary>
    Task<bool> CompleteUserTaskAsync(long userTaskKey, object variables, CancellationToken cancellationToken);
}

/// <summary>An open Camunda user task — the unit of human work offered to a candidate group.</summary>
public sealed record CamundaUserTask(
    long UserTaskKey,
    string ElementId,
    string? Assignee,
    long ProcessInstanceKey,
    DateTimeOffset CreationDate,
    IReadOnlyList<string> CandidateGroups);

/// <summary>An activated Camunda job: the unit of work a service task hands to an external worker.</summary>
public sealed record CamundaJob(
    long JobKey,
    string Type,
    long ProcessInstanceKey,
    IReadOnlyDictionary<string, JsonElement> Variables,
    IReadOnlyDictionary<string, string> CustomHeaders)
{
    /// <summary>Reads a string process variable carried on the job, or null when absent.</summary>
    public string? GetString(string name) =>
        Variables.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Reads a task header (zeebe:taskHeaders on the BPMN task), or null when absent.</summary>
    public string? GetHeader(string name) =>
        CustomHeaders.TryGetValue(name, out var value) ? value : null;
}
