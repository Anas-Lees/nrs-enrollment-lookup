namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the correlation-id middleware: every response carries an X-Correlation-Id,
/// and an inbound one is echoed back (so a caller can tie its request to server logs/traces).
/// </summary>
public class CorrelationIdTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task Response_AlwaysCarries_CorrelationId()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains(Header), "Response should include an X-Correlation-Id header.");
        var value = response.Headers.GetValues(Header).Single();
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public async Task InboundCorrelationId_IsEchoed()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(Header, "trace-abc-123");

        var response = await client.SendAsync(request);

        Assert.Equal("trace-abc-123", response.Headers.GetValues(Header).Single());
    }
}
