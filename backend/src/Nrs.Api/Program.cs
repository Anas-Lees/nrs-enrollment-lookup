using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nrs.Api.Errors;
using Nrs.Api.Extensions;
using Nrs.Api.Middleware;
using Nrs.Infrastructure.Persistence;
using Nrs.Infrastructure.Seed;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string SpaCorsPolicy = "spa";

// Structured (JSON) logging with scopes outside development so logs are machine-parseable
// and carry the correlation id; the readable console stays in dev.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
}

// OpenTelemetry tracing + metrics (ASP.NET Core + outbound HTTP). The OTLP exporter is
// only added when an endpoint is configured, so local/dev runs produce traces without
// erroring on a missing collector.
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
    ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("nrs-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

// RFC-7807 problem details for every error (validation, not-found, unhandled), each
// enriched with a traceId/correlationId so a failed call can be tracked.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        if (context.HttpContext.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId.ToString();
        }
    });
builder.Services.AddExceptionHandler<NrsExceptionHandler>();

// Controllers + serialize enums as their string names (matches the OpenAPI contract).
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// First-party OpenAPI document generation (served at /openapi/v1.json), rendered by Scalar.
builder.Services.AddOpenApi();

// Application services (DbContext, repository, service).
builder.Services.AddNrsServices(builder.Configuration);

// Health checks for container/Kubernetes probes. The database check is tagged "ready"
// so readiness reflects DB connectivity while liveness only reflects the process.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NrsDbContext>("database", tags: ["ready"]);

// Optional Keycloak (OIDC/JWT) auth — off unless Auth:Enabled is true.
var authEnabled = builder.Services.AddNrsAuthentication(builder.Configuration);

// Rate limiting on lookups — throttle per operator (or per IP when anonymous) to blunt
// population enumeration / scraping. Sliding window; configurable for tuning and tests.
var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 60;
var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("lookup", httpContext =>
    {
        var partitionKey =
            httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirst("preferred_username")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        });
    });
});

// Allow the Angular dev server to call the API.
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Consistent RFC-7807 error responses first in the pipeline (unhandled -> 500 problem;
// StatusCodePages turns bare 401/403/404/429 into problem bodies too), then correlation id.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();

// API docs (OpenAPI + Scalar). Exposed in Development, or in other environments only when
// explicitly enabled via OpenApi:Enabled — and then behind authorization. Never anonymous
// outside development.
var exposeApiDocs = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("OpenApi:Enabled");
if (exposeApiDocs)
{
    var openApi = app.MapOpenApi();
    var scalar = app.MapScalarApiReference(options =>
        options.WithTitle("NRS Enrollment — Applicant Lookup API"));

    if (!app.Environment.IsDevelopment() && authEnabled)
    {
        openApi.RequireAuthorization();
        scalar.RequireAuthorization();
    }
    else
    {
        openApi.AllowAnonymous();
        scalar.AllowAnonymous();
    }
}

// Create/upgrade the schema and (optionally) seed sample data on startup. Auto-migrate
// runs in every environment (the demo stacks rely on it). Seeding the synthetic Bogus
// data, however, defaults ON outside Production and OFF in Production — so a real registry
// is never auto-filled with fake citizens. Demo stacks opt in via Database:SeedOnStartup.
if (builder.Configuration.GetValue<bool?>("Database:InitializeOnStartup") ?? true)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();

    // Both providers apply real EF migrations on startup. SQLite's set lives in
    // Nrs.Infrastructure; Oracle's lives in Nrs.Infrastructure.Migrations.Oracle.
    await db.Database.MigrateAsync();

    if (builder.Configuration.GetValue<bool?>("Database:SeedOnStartup") ?? !app.Environment.IsProduction())
    {
        await DataSeeder.SeedAsync(db);
    }
}

app.UseCors(SpaCorsPolicy);

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// After auth so the limiter can partition by the authenticated operator.
app.UseRateLimiter();

app.MapControllers();

// Health endpoints for probes — always reachable, even when auth is on.
//   /health/live  — liveness: the process is up (no dependency checks).
//   /health/ready — readiness: dependencies (the database) are reachable.
//   /health       — full check (back-compatible alias).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposed so the integration-test project (WebApplicationFactory<Program>) can reference it.
public partial class Program;
