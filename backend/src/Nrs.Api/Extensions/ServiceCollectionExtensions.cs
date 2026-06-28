using Microsoft.EntityFrameworkCore;
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
}
