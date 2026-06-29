using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Like <see cref="NrsApiFactory"/> but with a deliberately tiny rate limit (3 per window)
/// so a test can trip the limiter quickly and assert a 429.
/// </summary>
public class NrsApiRateLimitFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public NrsApiRateLimitFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("RateLimiting:PermitLimit", "3");
        builder.UseSetting("RateLimiting:WindowSeconds", "60");

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
