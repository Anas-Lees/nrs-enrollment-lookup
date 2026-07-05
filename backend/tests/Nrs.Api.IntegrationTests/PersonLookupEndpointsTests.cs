using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the lookup endpoints: HTTP -> controller -> service -> repository
/// -> EF Core -> in-memory SQLite, against the real (seeded) application.
/// </summary>
public class PersonLookupEndpointsTests : IClassFixture<NrsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public PersonLookupEndpointsTests(NrsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [OracleFact]
    public async Task Search_ReturnsOk_AndPagedEnvelope()
    {
        var response = await _client.GetAsync("/api/v1/persons/search?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<PersonSummary>>(JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(100, page!.TotalCount);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.PageSize);
    }

    [OracleFact]
    public async Task Search_FiltersByNationality()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<PersonSummary>>(
            "/api/v1/persons/search?nationality=OMN&pageSize=100", JsonOptions);

        Assert.NotNull(page);
        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, item => Assert.Equal("OMN", item.NationalityCode));
    }

    [OracleFact]
    public async Task Search_PartialName_ReturnsMatches()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<PersonSummary>>(
            "/api/v1/persons/search?name=al&pageSize=5", JsonOptions);

        Assert.NotNull(page);
        Assert.True(page!.TotalCount > 0, "Expected a partial name match for 'al' to return results.");
    }

    [OracleFact]
    public async Task Search_RejectsOutOfRangePageSize_With400()
    {
        // pageSize is documented (and now validated) as 1..100; out of range is a 400.
        var response = await _client.GetAsync("/api/v1/persons/search?pageSize=9999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [OracleFact]
    public async Task Search_SerializesEnumsAsStrings()
    {
        var response = await _client.GetAsync("/api/v1/persons/search?pageSize=1");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();

        // Enum must be a string name (e.g. "ACTIVE"), not a number like "status":2.
        Assert.Matches(new Regex("\"status\":\"[A-Z]"), body);
    }

    [OracleFact]
    public async Task GetByCrn_ReturnsProfile_WithDocuments()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<PersonSummary>>(
            "/api/v1/persons/search?pageSize=1", JsonOptions);

        Assert.NotNull(page);
        var crn = page!.Items.Single().CivilNumber;

        var response = await _client.GetAsync($"/api/v1/persons/{crn}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var person = await response.Content.ReadFromJsonAsync<PersonProfile>(JsonOptions);

        Assert.NotNull(person);
        Assert.Equal(crn, person!.CivilNumber);
        Assert.True(person.IdCards.Length >= 1, "Expected the person to have at least one ID card.");
        Assert.True(person.Passports.Length >= 1, "Expected the person to have at least one passport.");
    }

    [OracleFact]
    public async Task GetByCrn_Returns404_ForUnknownCrn()
    {
        var response = await _client.GetAsync("/api/v1/persons/00000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Local deserialization shapes (status read as string to assert on enum names) ---

    private sealed record PagedResult<T>
    {
        public List<T> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    private sealed record PersonSummary
    {
        public string CivilNumber { get; init; } = null!;
        public string NationalityCode { get; init; } = null!;
        public string Status { get; init; } = null!;
    }

    private sealed record PersonProfile
    {
        public string CivilNumber { get; init; } = null!;
        public IdCard[] IdCards { get; init; } = [];
        public Passport[] Passports { get; init; } = [];
    }

    private sealed record IdCard
    {
        public string CardNumber { get; init; } = null!;
    }

    private sealed record Passport
    {
        public string PassportNumber { get; init; } = null!;
    }
}
