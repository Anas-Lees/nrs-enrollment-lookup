using System.Net;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the search endpoint rejects malformed query parameters with 400, and accepts
/// the valid edge cases (no filters, partial CRN prefix).
/// </summary>
public class SearchValidationTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [OracleTheory]
    [InlineData("/api/v1/persons/search?page=0")] // page below 1
    [InlineData("/api/v1/persons/search?pageSize=0")] // page size below 1
    [InlineData("/api/v1/persons/search?pageSize=101")] // page size above max
    [InlineData("/api/v1/persons/search?crn=abc")] // CRN not digits
    [InlineData("/api/v1/persons/search?crn=1234567890")] // CRN too long (10 digits)
    [InlineData("/api/v1/persons/search?nationality=OMANI")] // nationality not 3 letters
    [InlineData("/api/v1/persons/search?nationality=12")] // nationality not letters
    [InlineData("/api/v1/persons/search?dob=not-a-date")] // unparseable date
    public async Task Search_RejectsMalformedInput_With400(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [OracleTheory]
    [InlineData("/api/v1/persons/search")] // no filters → all (paged)
    [InlineData("/api/v1/persons/search?crn=12")] // partial CRN prefix is allowed
    [InlineData("/api/v1/persons/search?nationality=omn")] // case-insensitive 3 letters
    [InlineData("/api/v1/persons/search?page=1&pageSize=100")] // boundary page size
    public async Task Search_AcceptsValidInput_With200(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
