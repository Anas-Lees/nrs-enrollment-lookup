using Microsoft.AspNetCore.Hosting;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Runs the app in Production with NO seed opt-in (the shipped default), to prove the
/// safety guarantee: the schema is migrated but the synthetic Bogus data is NOT seeded —
/// a real registry is never auto-filled with fake citizens.
/// </summary>
public class NrsApiProdNoSeedFactory : OracleWebApplicationFactory
{
    protected override void ConfigureScenario(IWebHostBuilder builder)
    {
        // Production, and deliberately NOT setting Database:SeedOnStartup — so the
        // Production default (no seeding) applies.
        builder.UseEnvironment("Production");
    }
}
