using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Oracle.EntityFrameworkCore.Infrastructure;
using Nrs.Api.Auth;
using Nrs.Api.Filters;
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
    /// <summary>Keycloak realm role required to use the applicant-lookup endpoints.</summary>
    public const string OperatorRole = "operator";

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
                    oracle =>
                    {
                        // Target 19c SQL so generated queries avoid 23c-only features (e.g. the
                        // boolean literals that Oracle XE 21c rejects). Raise for newer servers.
                        oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
                        // Oracle migrations live in their own assembly (see ADR/A8).
                        oracle.MigrationsAssembly(NrsDbContextFactory.OracleMigrationsAssembly);
                        // Resilience: cap query time and retry transient connectivity blips.
                        oracle.CommandTimeout(30);
                        oracle.ExecutionStrategy(dependencies => new OracleTransientRetryStrategy(dependencies));
                    });
            }
            else
            {
                options.UseSqlite(configuration.GetConnectionString("Default") ?? "Data Source=nrs.db");
            }
        });

        // Distributed cache for hot profile reads: Redis when a connection string is
        // configured (shared across API instances), otherwise an in-process cache so local
        // dev and tests need no external dependency. Same IDistributedCache contract either way.
        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<IPersonRepository, PersonRepository>();

        // The application service, wrapped in a cache-aside decorator. The decorator sits
        // BELOW the controller's audit filter, so caching never bypasses the audit trail —
        // a cache hit skips the DB call, not the "who looked up whom" record.
        services.AddScoped<PersonLookupService>();
        services.AddScoped<IPersonLookupService>(sp =>
            new CachedPersonLookupService(
                sp.GetRequiredService<PersonLookupService>(),
                sp.GetRequiredService<IDistributedCache>()));

        // Audit trail: the logger persists records; the MVC filter captures every lookup.
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<AuditActionFilter>();

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
        // Optional: where the API fetches OIDC discovery/JWKS from. Lets the browser and the
        // API reach Keycloak via different hostnames (e.g. localhost:8081 vs keycloak:8080 in
        // Docker) while the token issuer stays consistent.
        var metadataAddress = configuration["Auth:MetadataAddress"];
        // Secure by default: require HTTPS for OIDC metadata/JWKS. Deployments that reach
        // Keycloak over internal plain HTTP (e.g. docker compose) opt out explicitly.
        var requireHttpsMetadata = configuration.GetValue<bool?>("Auth:RequireHttpsMetadata") ?? true;

        // Fail fast: with auth on we always validate the audience, so it must be configured.
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Auth:Audience must be configured when Auth:Enabled is true — the API validates the token audience.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Issuer and signing keys come from OIDC discovery; pin audience, lifetime
                    // and signature, and keep clock skew tight.
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(60),
                };
                options.Events = new JwtBearerEvents
                {
                    // Keycloak nests realm roles under realm_access.roles; promote them to
                    // standard role claims so authorization policies (RequireRole) can see them.
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity)
                        {
                            var realmAccess = identity.FindFirst(KeycloakRoleClaims.RealmAccessClaim)?.Value;
                            foreach (var role in KeycloakRoleClaims.FromRealmAccess(realmAccess))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            // Once auth is on, every endpoint (except those marked AllowAnonymous, e.g.
            // /health) requires an authenticated user WITH the operator role. This is the
            // authorization control: authentication alone is not enough.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(OperatorRole)
                .Build();
        });

        return true;
    }
}
