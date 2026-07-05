using System.Net;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the split health endpoints: liveness (process), readiness (database), and the
/// back-compatible aggregate — all anonymous, all healthy against the seeded test database.
/// </summary>
public class HealthCheckTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [OracleTheory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task HealthEndpoints_ReturnHealthy(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }
}
