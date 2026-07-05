using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// <see cref="ICamundaClient"/> over the Camunda 8 <c>/v2</c> REST API using a typed
/// <see cref="HttpClient"/>. Request bodies are plain JSON (camelCase); the API returns the
/// 64-bit keys as JSON strings, so they are parsed with <see cref="long.Parse(string, IFormatProvider)"/>.
/// </summary>
public sealed class CamundaClient(HttpClient http) : ICamundaClient
{
    // Web defaults => camelCase, which is exactly what the /v2 API expects.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync("v2/topology", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false; // request timed out while the broker was still booting
        }
    }

    public async Task<long> DeployResourceAsync(string resourceName, byte[] content, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        // The /v2/deployments endpoint reads one-or-more parts named "resources".
        form.Add(file, "resources", resourceName);

        using var response = await http.PostAsync("v2/deployments", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var key = doc.RootElement
            .GetProperty("deployments")[0]
            .GetProperty("processDefinition")
            .GetProperty("processDefinitionKey")
            .GetString();
        return ParseKey(key);
    }

    public async Task<long> CreateProcessInstanceAsync(string processDefinitionId, object variables, CancellationToken cancellationToken)
    {
        var payload = new { processDefinitionId, variables };
        using var response = await http.PostAsJsonAsync("v2/process-instances", payload, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ParseKey(doc.RootElement.GetProperty("processInstanceKey").GetString());
    }

    public async Task<IReadOnlyList<CamundaJob>> ActivateJobsAsync(
        string jobType, int maxJobs, TimeSpan lockTimeout, TimeSpan requestTimeout, string worker, CancellationToken cancellationToken)
    {
        var payload = new
        {
            type = jobType,
            maxJobsToActivate = maxJobs,
            timeout = (long)lockTimeout.TotalMilliseconds,      // how long the job stays locked to us
            requestTimeout = (long)requestTimeout.TotalMilliseconds, // long-poll window
            worker,
        };

        using var response = await http.PostAsJsonAsync("v2/jobs/activation", payload, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!doc.RootElement.TryGetProperty("jobs", out var jobsElement) || jobsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var jobs = new List<CamundaJob>(jobsElement.GetArrayLength());
        foreach (var job in jobsElement.EnumerateArray())
        {
            var variables = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (job.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in vars.EnumerateObject())
                {
                    // Clone: the JsonDocument is disposed when this method returns.
                    variables[property.Name] = property.Value.Clone();
                }
            }

            jobs.Add(new CamundaJob(
                ParseKey(job.GetProperty("jobKey").GetString()),
                job.GetProperty("type").GetString()!,
                ParseKey(job.GetProperty("processInstanceKey").GetString()),
                variables));
        }

        return jobs;
    }

    public async Task CompleteJobAsync(long jobKey, object? variables, CancellationToken cancellationToken)
    {
        var payload = new { variables = variables ?? new { } };
        using var response = await http.PostAsJsonAsync(
            $"v2/jobs/{jobKey.ToString(CultureInfo.InvariantCulture)}/completion", payload, Json, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> CorrelateMessageAsync(string name, string correlationKey, object variables, CancellationToken cancellationToken)
    {
        var payload = new { name, correlationKey, variables };
        using var response = await http.PostAsJsonAsync("v2/messages/correlation", payload, Json, cancellationToken);

        // 404 = no instance is subscribed to this message yet. Just after a process starts there is
        // a brief window where the enrollment already reads UNDER_REVIEW but the message-catch
        // subscription has not opened — a transient race, not a hard failure. Let the caller retry.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    private static long ParseKey(string? key) =>
        long.Parse(key ?? throw new InvalidOperationException("Camunda returned a null key."), CultureInfo.InvariantCulture);
}
