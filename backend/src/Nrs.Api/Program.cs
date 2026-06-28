using System.Data.Common;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Nrs.Api.Extensions;
using Nrs.Api.Middleware;
using Nrs.Infrastructure.Persistence;
using Nrs.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

const string SpaCorsPolicy = "spa";

// Controllers + serialize enums as their string names (matches the OpenAPI contract).
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI / Swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NRS Enrollment — Applicant Lookup API",
        Version = "v1",
        Description = "Search for persons and retrieve their full profile (ID cards and passports).",
    });

    // Surface the XML doc comments from the API and Application assemblies in Swagger.
    foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Nrs.Application.Dtos.PersonDto).Assembly })
    {
        var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        if (File.Exists(xml))
        {
            options.IncludeXmlComments(xml);
        }
    }
});

// Application services (DbContext, repository, service).
builder.Services.AddNrsServices(builder.Configuration);

// Health checks for container/Kubernetes probes.
builder.Services.AddHealthChecks();

// Optional Keycloak (OIDC/JWT) auth — off unless Auth:Enabled is true.
var authEnabled = builder.Services.AddNrsAuthentication(builder.Configuration);

// Allow the Angular dev server to call the API.
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Consistent error responses first in the pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

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
        // Create the schema from the model. Guarded: EnsureCreated's existence check can
        // be unreliable against a persistent Oracle volume on restart, so if the schema is
        // already there we log and continue (the seeder below is idempotent).
        try
        {
            await db.Database.EnsureCreatedAsync();
        }
        catch (DbException ex)
        {
            app.Logger.LogWarning(ex, "Schema creation skipped; it appears to already exist.");
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

app.MapControllers();

// Liveness/readiness endpoint for probes — always reachable, even when auth is on.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposed so the integration-test project (WebApplicationFactory<Program>) can reference it.
public partial class Program;
