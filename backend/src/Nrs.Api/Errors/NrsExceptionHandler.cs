using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Nrs.Api.Errors;

/// <summary>
/// Turns unhandled exceptions into an RFC-7807 ProblemDetails (500) via the shared
/// ProblemDetailsService, so error bodies are consistent with validation/not-found
/// responses (and carry the same traceId). Replaces the bespoke error middleware.
/// </summary>
public sealed partial class NrsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<NrsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandled(logger, httpContext.Request.Method, httpContext.Request.Path, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, string method, string path, Exception ex);
}
