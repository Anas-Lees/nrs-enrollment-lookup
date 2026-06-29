using System.Diagnostics;

namespace Nrs.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation id: reuses an inbound <c>X-Correlation-Id</c>
/// (or the active trace id), echoes it on the response, tags the current trace, and adds
/// it to the logging scope so every log line for the request can be tied together.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var provided) && !string.IsNullOrWhiteSpace(provided)
                ? provided.ToString()
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
