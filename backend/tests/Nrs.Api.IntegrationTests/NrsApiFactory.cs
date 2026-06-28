using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Spins up the real API in-process (TestServer) backed by a shared in-memory SQLite
/// database. The connection is opened for the lifetime of the factory so the in-memory
/// database survives across DI scopes; the app's own Development startup then migrates
/// and seeds it.
/// </summary>
public class NrsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public NrsApiFactory()
    {
        // Open before the host builds so the in-memory DB exists when startup migrates/seeds.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development triggers MigrateAsync + DataSeeder.SeedAsync at startup.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove every DbContextOptions<NrsDbContext> registration (and any pooling/internal
            // options) the app added, plus the NrsDbContext itself, so our SQLite wiring wins.
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
