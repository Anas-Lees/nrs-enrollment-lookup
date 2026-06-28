using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=nrs.db";

        services.AddDbContext<NrsDbContext>(options => options.UseSqlite(connectionString));

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
