namespace Nrs.Api.Features.Reports;

/// <summary>
/// Minimal-API route for the enrollment analytics dashboard, under <c>/api/v1/reports</c>.
/// Aggregate figures only (no PII), but management information all the same — gated to the
/// supervisor role ("CanSupervise" policy) when auth is on.
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app, bool enforceRoles)
    {
        var group = app.MapGroup("/api/v1/reports")
            .WithTags("Reports")
            .RequireRateLimiting("lookup");
        if (enforceRoles)
        {
            group.RequireAuthorization("CanSupervise");
        }

        group.MapGet("enrollment-summary", async (
                int? days,
                ReportsFeature.Handler handler,
                CancellationToken cancellationToken) =>
            {
                var report = await handler.HandleAsync(days ?? 30, cancellationToken);
                return Results.Ok(report);
            })
            .WithSummary("Enrollment analytics for the last N days (default 30)")
            .Produces<ReportsFeature.EnrollmentReportDto>();

        return app;
    }
}
