using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies production-shaped behaviour: under Production the schema is migrated on startup
/// and — when seeding is explicitly opted in (as the demo stacks do, since seeding defaults
/// OFF in Production) — the synthetic population is seeded, while the API docs stay hidden.
/// </summary>
public class ProductionModeTests(NrsApiProdFactory factory) : IClassFixture<NrsApiProdFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [OracleFact]
    public async Task Startup_MigratesAndSeeds_InProduction_WhenOptedIn()
    {
        // The prod factory opts in to seeding; the migrated schema + seed mean search
        // returns the seeded population (not just a 200 on an empty table).
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/v1/persons/search?pageSize=1");

        Assert.True(page.GetProperty("totalCount").GetInt32() > 0);
    }

    [OracleFact]
    public async Task ApiDocs_AreNotExposed_ByDefault_InProduction()
    {
        var scalar = await _client.GetAsync("/scalar");
        var openApi = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
    }
}

/// <summary>
/// The safety guarantee: under Production with no explicit opt-in (the shipped default),
/// the schema is migrated but the synthetic Bogus data is NOT seeded.
/// </summary>
public class ProductionSeedSafetyTests(NrsApiProdNoSeedFactory factory) : IClassFixture<NrsApiProdNoSeedFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [OracleFact]
    public async Task Production_DoesNotAutoSeed_FakeData_ByDefault()
    {
        // Schema migrated (search succeeds) but empty — no fake citizens were written.
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/v1/persons/search?pageSize=1");

        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }
}
