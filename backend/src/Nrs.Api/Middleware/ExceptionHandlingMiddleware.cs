using Microsoft.AspNetCore.Mvc;

namespace Nrs.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns a consistent RFC-7807 ProblemDetails
/// response instead of a raw stack trace.
/// </summary>
public partial class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            LogUnhandledException(logger, context.Request.Method, context.Request.Path, ex);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Instance = context.Request.Path,
            };

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, string method, string path, Exception ex);
}
