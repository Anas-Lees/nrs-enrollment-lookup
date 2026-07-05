using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Base for the integration-test factories. Points the real app at the shared Testcontainers
/// Oracle database (by overriding ConnectionStrings:Default) and resets that schema before
/// each test class, so every class starts clean — the app's own startup then migrates and
/// seeds it, exactly as in production. Subclasses only add their per-scenario environment,
/// settings and services via <see cref="ConfigureScenario"/>.
///
/// The test run is serialized (see xunit.runner.json) so classes never collide on the shared
/// schema. When Docker is absent the tests are skipped by [OracleFact]/[OracleTheory] and
/// this setup is a no-op.
/// </summary>
public abstract class OracleWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _connectionString = string.Empty;

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (!OracleTestcontainer.IsDockerAvailable)
        {
            return;
        }

        _connectionString = await OracleTestcontainer.GetConnectionStringAsync();
        await OracleTestcontainer.ResetSchemaAsync(_connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The app reads ConnectionStrings:Default and calls UseOracle itself, so we only need
        // to point it at the container — no DbContext re-registration required.
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        ConfigureScenario(builder);
    }

    /// <summary>Per-factory environment, settings and services (auth, rate limits, seeding).</summary>
    protected abstract void ConfigureScenario(IWebHostBuilder builder);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
