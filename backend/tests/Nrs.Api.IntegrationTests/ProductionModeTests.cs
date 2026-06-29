using System.Net;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies production-shaped behaviour: the database is migrated and seeded on startup
/// (so search works) even outside Development, and the API docs are not exposed by default.
/// </summary>
public class ProductionModeTests(NrsApiProdFactory factory) : IClassFixture<NrsApiProdFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Startup_MigratesAndSeeds_InProduction()
    {
        // Seed ran outside Development → search returns the seeded population.
        var response = await _client.GetAsync("/api/v1/persons/search?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiDocs_AreNotExposed_ByDefault_InProduction()
    {
        var scalar = await _client.GetAsync("/scalar");
        var openApi = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
    }
}
