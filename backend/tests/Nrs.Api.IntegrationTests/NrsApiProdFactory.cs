using Microsoft.AspNetCore.Hosting;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Runs the app in the Production environment (still on the shared Oracle container) to verify
/// the production-shaped behaviour: startup migrate+seed runs (not gated to Development), and
/// the API docs are not exposed anonymously.
/// </summary>
public class NrsApiProdFactory : OracleWebApplicationFactory
{
    protected override void ConfigureScenario(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // Seeding defaults OFF under Production; opt in explicitly, exactly as the
        // docker-compose demo stack does, so this factory exercises that real path.
        builder.UseSetting("Database:SeedOnStartup", "true");
    }
}
