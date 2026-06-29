using System.Net;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the lookup endpoints are rate-limited: with a 3-per-window limit, the first
/// three requests succeed and further requests are throttled with 429.
/// </summary>
public class RateLimitTests(NrsApiRateLimitFactory factory) : IClassFixture<NrsApiRateLimitFactory>
{
    [Fact]
    public async Task Search_Throttles_AfterPermitLimit()
    {
        var client = factory.CreateClient();
        var codes = new List<HttpStatusCode>();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/api/v1/persons/search?pageSize=1");
            codes.Add(response.StatusCode);
        }

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }
}
