using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Runs the app in Production with NO seed opt-in (the shipped default), to prove the
/// safety guarantee: the schema is migrated but the synthetic Bogus data is NOT seeded —
/// a real registry is never auto-filled with fake citizens.
/// </summary>
public class NrsApiProdNoSeedFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public NrsApiProdNoSeedFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, and deliberately NOT setting Database:SeedOnStartup — so the
        // Production default (no seeding) applies.
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NrsDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(NrsDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<NrsDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
