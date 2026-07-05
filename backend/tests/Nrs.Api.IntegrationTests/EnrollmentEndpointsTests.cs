using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the enrollment vertical slices: HTTP -> minimal-API endpoint -> slice
/// handler -> EF Core -> Oracle. The test host has no broker configured, so the no-op
/// publisher is used and a created enrollment stays SUBMITTED (the RabbitMQ review transition
/// is exercised against a live broker, not here).
/// </summary>
public class EnrollmentEndpointsTests : IClassFixture<NrsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public EnrollmentEndpointsTests(NrsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object NewApplicant(string type = "NEW_CARD", string? crn = null) => new
    {
        civilNumber = crn,
        firstNameEn = "Salim",
        familyNameEn = "Al Harthy",
        firstNameAr = "سالم",
        familyNameAr = "الحارثي",
        dateOfBirth = "1990-05-14",
        nationalityCode = "OMN",
        type,
        notes = "Walk-in at Muscat HQ",
    };

    [OracleFact]
    public async Task Create_ReturnsCreated_WithReferenceAndSubmittedStatus()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto!.Id);
        Assert.StartsWith("ENR-", dto.ReferenceNumber);
        Assert.Equal("SUBMITTED", dto.Status);
        Assert.Equal("NEW_CARD", dto.Type);
        Assert.Equal("Salim", dto.FirstNameEn);
    }

    [OracleFact]
    public async Task Create_NationalityIsNormalisedToUpper()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            firstNameEn = "Noor",
            familyNameEn = "Al Balushi",
            firstNameAr = "نور",
            familyNameAr = "البلوشي",
            dateOfBirth = "1993-01-09",
            nationalityCode = "omn",
            type = "RENEWAL",
        });

        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        Assert.Equal("OMN", dto!.NationalityCode);
    }

    [OracleFact]
    public async Task Create_InvalidBody_Returns400_WithValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            firstNameEn = "",              // required
            familyNameEn = "X",
            firstNameAr = "",              // required
            familyNameAr = "ص",
            dateOfBirth = "2999-01-01",    // not a past date
            nationalityCode = "OM",        // must be 3 letters
            type = "NEW_CARD",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("errors", body);
    }

    [OracleFact]
    public async Task Get_ReturnsCreatedEnrollment()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant("REPLACEMENT")))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);

        var fetched = await _client.GetFromJsonAsync<EnrollmentDto>($"/api/v1/enrollments/{created!.Id}", JsonOptions);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.ReferenceNumber, fetched.ReferenceNumber);
        Assert.Equal("REPLACEMENT", fetched.Type);
    }

    [OracleFact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/enrollments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [OracleFact]
    public async Task Update_ChangesEditableFields()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);

        var response = await _client.PutAsJsonAsync($"/api/v1/enrollments/{created!.Id}", new
        {
            civilNumber = "12345678",
            firstNameEn = "Salim",
            familyNameEn = "Al Harthy",
            firstNameAr = "سالم",
            familyNameAr = "الحارثي",
            dateOfBirth = "1990-05-14",
            nationalityCode = "OMN",
            type = "CORRECTION",
            notes = "Corrected surname spelling",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        Assert.Equal("CORRECTION", updated!.Type);
        Assert.Equal("12345678", updated.CivilNumber);
        Assert.Equal("Corrected surname spelling", updated.Notes);
    }

    [OracleFact]
    public async Task Update_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/enrollments/{Guid.NewGuid()}", NewApplicant());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [OracleFact]
    public async Task List_ReturnsPagedEnvelope_ContainingCreatedItem()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);

        var page = await _client.GetFromJsonAsync<PagedResult<EnrollmentSummary>>(
            "/api/v1/enrollments?page=1&pageSize=50", JsonOptions);

        Assert.NotNull(page);
        Assert.True(page!.TotalCount >= 1);
        Assert.Contains(page.Items, e => e.ReferenceNumber == created!.ReferenceNumber);
    }

    [OracleFact]
    public async Task List_FilterByStatus_ReturnsOnlyThatStatus()
    {
        await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant());

        var page = await _client.GetFromJsonAsync<PagedResult<EnrollmentSummary>>(
            "/api/v1/enrollments?status=SUBMITTED&pageSize=50", JsonOptions);

        Assert.NotNull(page);
        Assert.All(page!.Items, e => Assert.Equal("SUBMITTED", e.Status));
    }

    [OracleFact]
    public async Task Enrollment_SerializesEnumsAsStrings()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant());
        var body = await response.Content.ReadAsStringAsync();

        // type / status must be string names, not numbers.
        Assert.Contains("\"status\":\"SUBMITTED\"", body);
        Assert.Contains("\"type\":\"NEW_CARD\"", body);
    }

    // --- Local deserialization shapes ---

    private sealed record EnrollmentDto
    {
        public Guid Id { get; init; }
        public string ReferenceNumber { get; init; } = null!;
        public string? CivilNumber { get; init; }
        public string FirstNameEn { get; init; } = null!;
        public string NationalityCode { get; init; } = null!;
        public string Type { get; init; } = null!;
        public string Status { get; init; } = null!;
        public string? Notes { get; init; }
    }

    private sealed record EnrollmentSummary
    {
        public string ReferenceNumber { get; init; } = null!;
        public string Status { get; init; } = null!;
    }

    private sealed record PagedResult<T>
    {
        public List<T> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }
}
