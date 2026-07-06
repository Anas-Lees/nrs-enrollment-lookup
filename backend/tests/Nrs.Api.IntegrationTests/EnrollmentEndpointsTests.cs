using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

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
    private readonly NrsApiFactory _factory;

    public EnrollmentEndpointsTests(NrsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Force an enrollment to UNDER_REVIEW directly in the database. The test host has no Camunda
    /// or broker, so nothing advances SUBMITTED -> UNDER_REVIEW on its own; this stands in for that
    /// transition so the decision endpoint (which requires UNDER_REVIEW) can be exercised.
    /// </summary>
    private async Task SetUnderReviewAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == id);
        enrollment.Status = EnrollmentStatus.UNDER_REVIEW;
        await db.SaveChangesAsync();
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
    public async Task Decide_Approve_WhenUnderReview_Returns200_Approved()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        await SetUnderReviewAsync(created!.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/enrollments/{created.Id}/decision", new { approved = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        Assert.Equal("APPROVED", dto!.Status);
    }

    [OracleFact]
    public async Task Decide_Reject_WhenUnderReview_Returns200_Rejected_WithAudit()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        await SetUnderReviewAsync(created!.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/enrollments/{created.Id}/decision",
            new { approved = false, notes = "Photo does not match the registry record." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        Assert.Equal("REJECTED", dto!.Status);
        // The decision audit trail: who, when and why.
        Assert.Equal("anonymous", dto.DecidedBy);
        Assert.NotNull(dto.DecidedAtUtc);
        Assert.Equal("Photo does not match the registry record.", dto.DecisionNotes);
    }

    [OracleFact]
    public async Task Decide_Reject_WithoutReason_Returns400()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        await SetUnderReviewAsync(created!.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/enrollments/{created.Id}/decision", new { approved = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [OracleFact]
    public async Task ReviewTasks_WithNoEngine_ListsUnderReviewEnrollments()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);
        await SetUnderReviewAsync(created!.Id);

        var response = await _client.GetAsync("/api/v1/review-tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // No Camunda in the test host: the list degrades to UNDER_REVIEW rows, keyless.
        Assert.Contains(created.ReferenceNumber, body);
        Assert.Contains("\"userTaskKey\":null", body);
    }

    [OracleFact]
    public async Task Notifications_ListAndMarkAllRead_Work()
    {
        var list = await _client.GetFromJsonAsync<NotificationList>(
            "/api/v1/notifications?limit=5", JsonOptions);
        Assert.NotNull(list);

        var readAll = await _client.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, readAll.StatusCode);

        var after = await _client.GetFromJsonAsync<NotificationList>(
            "/api/v1/notifications", JsonOptions);
        Assert.Equal(0, after!.UnreadCount);
    }

    [OracleFact]
    public async Task Decide_WhenNotUnderReview_Returns409()
    {
        // A freshly created enrollment is SUBMITTED, not UNDER_REVIEW.
        var created = await (await _client.PostAsJsonAsync("/api/v1/enrollments", NewApplicant()))
            .Content.ReadFromJsonAsync<EnrollmentDto>(JsonOptions);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/enrollments/{created!.Id}/decision", new { approved = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [OracleFact]
    public async Task Decide_UnknownId_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/enrollments/{Guid.NewGuid()}/decision", new { approved = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        public string? DecidedBy { get; init; }
        public DateTimeOffset? DecidedAtUtc { get; init; }
        public string? DecisionNotes { get; init; }
    }

    private sealed record NotificationList
    {
        public int UnreadCount { get; init; }
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
