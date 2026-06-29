namespace Nrs.Api.Middleware;

/// <summary>
/// Adds baseline security response headers to every API response. The SPA's nginx sets the
/// full set (incl. a strict Content-Security-Policy) for the rendered app; the API returns
/// JSON (and the Scalar docs page), so a strict CSP is intentionally NOT set here to avoid
/// breaking Scalar. HSTS is harmless over HTTP and takes effect once traffic is HTTPS.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await next(context);
    }
}
