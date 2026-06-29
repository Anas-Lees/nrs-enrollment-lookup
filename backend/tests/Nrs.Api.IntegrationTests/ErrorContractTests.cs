using System.Net;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies errors are RFC-7807 ProblemDetails (application/problem+json) carrying a
/// traceId — for not-found, validation, and unknown routes alike.
/// </summary>
public class ErrorContractTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task AssertProblemWithTraceId(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
        Assert.Contains("json", mediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.Equal((int)expected, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("title", out _));
        Assert.True(doc.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    [Fact]
    public async Task NotFound_IsProblemJson_WithTraceId()
    {
        var response = await _client.GetAsync("/api/v1/persons/00000000");
        await AssertProblemWithTraceId(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidationError_IsProblemJson_WithTraceId()
    {
        var response = await _client.GetAsync("/api/v1/persons/search?pageSize=9999");
        await AssertProblemWithTraceId(response, HttpStatusCode.BadRequest);
    }
}
