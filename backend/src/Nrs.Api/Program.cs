using System.Data.Common;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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

// Consistent error responses first in the pipeline, then correlation id for everything after.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    // OpenAPI doc + Scalar API reference UI (anonymous, like the docs tool it replaces).
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.WithTitle("NRS Enrollment — Applicant Lookup API"))
        .AllowAnonymous();

    // Create/upgrade the schema and seed sample data on startup (dev convenience).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();
    // The committed migration is SQLite-specific; for other providers (Oracle) create
    // the schema from the model. Real Oracle environments would use Oracle migrations.
    if (db.Database.IsSqlite())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        // Guarded: EnsureCreated's existence check can be unreliable against a persistent
        // Oracle volume on restart, so if the schema is already there we log and continue
        // (the seeder below is idempotent).
        try
        {
            await db.Database.EnsureCreatedAsync();
        }
        catch (DbException ex)
        {
            // Startup-only diagnostic; a LoggerMessage delegate would be overkill here.
#pragma warning disable CA1848
            app.Logger.LogWarning(ex, "Schema creation skipped; it appears to already exist.");
#pragma warning restore CA1848
        }
    }
    await DataSeeder.SeedAsync(db);
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
