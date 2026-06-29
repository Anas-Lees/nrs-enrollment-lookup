using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Like <see cref="NrsApiFactory"/> but with authentication ENABLED, so the operator-role
/// authorization policy is in force. The real Keycloak JWT scheme is replaced by a header
/// driven <see cref="TestAuthHandler"/> set as the default scheme, letting tests assert
/// 401 (no identity), 403 (wrong role) and 200 (operator) through the real pipeline.
/// </summary>
public class NrsApiAuthFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public NrsApiAuthFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Turn the optional Keycloak auth ON (adds the operator-role fallback policy and the
        // authentication/authorization middleware). No Authority/MetadataAddress is set, so
        // nothing tries to reach a live Keycloak — the Test scheme below does the auth.
        builder.UseSetting("Auth:Enabled", "true");
        builder.UseSetting("Auth:Audience", "nrs-api");

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

            // Make the header-driven Test scheme the default so the fallback policy uses it.
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
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
