using Microsoft.AspNetCore.Hosting;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Spins up the real API in-process (TestServer) backed by the shared Testcontainers Oracle
/// database. The app's own Development startup migrates and seeds the freshly-reset schema.
/// </summary>
public class NrsApiFactory : OracleWebApplicationFactory
{
    protected override void ConfigureScenario(IWebHostBuilder builder)
    {
        // Development triggers MigrateAsync + DataSeeder.SeedAsync at startup.
        builder.UseEnvironment("Development");
    }
}
