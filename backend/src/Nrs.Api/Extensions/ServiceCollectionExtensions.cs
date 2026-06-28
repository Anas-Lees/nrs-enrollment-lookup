using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Oracle.EntityFrameworkCore.Infrastructure;
using Nrs.Application.Interfaces;
using Nrs.Application.Services;
using Nrs.Infrastructure.Persistence;
using Nrs.Infrastructure.Repositories;

namespace Nrs.Api.Extensions;

/// <summary>
/// Composition-root helpers that register the application's services, keeping
/// <c>Program.cs</c> small and readable.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DbContext (SQLite for local development — see ADR 0003), the
    /// repository, and the application service.
    /// </summary>
    public static IServiceCollection AddNrsServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Provider is config-driven: SQLite for local dev (default), Oracle for higher
        // environments (ADR 0003). The fluent mappings are provider-neutral
        // (IsUnicode(true) → NVARCHAR2 on Oracle), so only the registration changes.
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

        services.AddDbContext<NrsDbContext>(options =>
        {
            if (provider.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                options.UseOracle(
                    configuration.GetConnectionString("Default"),
                    // Target 19c SQL so generated queries avoid 23c-only features (e.g. the
                    // boolean literals that Oracle XE 21c rejects). Raise for newer servers.
                    oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
            }
            else
            {
                options.UseSqlite(configuration.GetConnectionString("Default") ?? "Data Source=nrs.db");
            }
        });

        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IPersonLookupService, PersonLookupService>();

        return services;
    }

    /// <summary>
    /// Optionally wires Keycloak (OIDC/JWT) authentication. Controlled by the
    /// <c>Auth:Enabled</c> flag so the POC runs open by default; when enabled, every
    /// endpoint (except those marked AllowAnonymous) requires a valid bearer token.
    /// Returns whether authentication was enabled, so the pipeline can add the
    /// matching middleware. (Stretch goal — see ADR 0001 / the delivery playbook.)
    /// </summary>
    public static bool AddNrsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Auth:Enabled"))
        {
            return false;
        }

        var authority = configuration["Auth:Authority"];
        var audience = configuration["Auth:Audience"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                // Keycloak is typically reached over plain HTTP inside the cluster.
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                };
            });

        services.AddAuthorization(options =>
        {
            // Lock everything down by default once auth is on.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return true;
    }
}
