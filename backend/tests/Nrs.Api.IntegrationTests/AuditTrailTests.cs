using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>A row from GET /api/v1/audit/recent (Action read as a string).</summary>
internal sealed record AuditRow(
    long AuditId,
    DateTimeOffset TimestampUtc,
    string Actor,
    string Action,
    string? TargetCrn,
    string? Criteria,
    int? ResultCount,
    string? SourceIp);

/// <summary>
/// Verifies every lookup is recorded to the audit trail (open POC path → actor "anonymous").
/// </summary>
public class AuditTrailTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<IReadOnlyList<AuditRow>> RecentAsync(HttpClient client) =>
        await client.GetFromJsonAsync<List<AuditRow>>("/api/v1/audit/recent?take=200", JsonOptions)
        ?? [];

    [Fact]
    public async Task Search_WritesAuditEntry_WithCriteriaAndCount()
    {
        var client = factory.CreateClient();

        var search = await client.GetAsync("/api/v1/persons/search?nationality=BHR&pageSize=5");
        search.EnsureSuccessStatusCode();

        var recent = await RecentAsync(client);
        var entry = recent.FirstOrDefault(a => a.Action == "SEARCH" && a.Criteria != null && a.Criteria.Contains("nat=BHR"));

        Assert.NotNull(entry);
        Assert.Equal("anonymous", entry!.Actor);
        Assert.NotNull(entry.ResultCount);
        Assert.Null(entry.TargetCrn);
    }

    [Fact]
    public async Task ProfileView_WritesAuditEntry_WithTargetCrn()
    {
        var client = factory.CreateClient();

        // Grab a real CRN from a search, then open the profile.
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/persons/search?pageSize=1");
        var crn = page.GetProperty("items")[0].GetProperty("civilNumber").GetString()!;

        var profile = await client.GetAsync($"/api/v1/persons/{crn}");
        profile.EnsureSuccessStatusCode();

        var recent = await RecentAsync(client);
        var entry = recent.FirstOrDefault(a => a.Action == "VIEW_PROFILE" && a.TargetCrn == crn);

        Assert.NotNull(entry);
        Assert.Equal("anonymous", entry!.Actor);
    }

    [Fact]
    public async Task ProfileView_IsAudited_OnEveryRequest_IncludingCacheHits()
    {
        var client = factory.CreateClient();

        // A stable CRN that other tests don't open.
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/persons/search?nationality=OMN&pageSize=10");
        var crn = page.GetProperty("items")[7].GetProperty("civilNumber").GetString()!;

        var before = (await RecentAsync(client)).Count(a => a.Action == "VIEW_PROFILE" && a.TargetCrn == crn);

        // View twice: the first populates the cache, the second is served from the cache.
        (await client.GetAsync($"/api/v1/persons/{crn}")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/v1/persons/{crn}")).EnsureSuccessStatusCode();

        var after = (await RecentAsync(client)).Count(a => a.Action == "VIEW_PROFILE" && a.TargetCrn == crn);

        // Caching must NOT suppress the audit trail — both views, cache hit included, are recorded.
        Assert.Equal(before + 2, after);
    }
}

/// <summary>Verifies the audited actor is the authenticated operator when auth is on.</summary>
public class AuditActorTests(NrsApiAuthFactory factory) : IClassFixture<NrsApiAuthFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SearchAsOperator_RecordsOperatorAsActor()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "operator");

        var search = await client.GetAsync("/api/v1/persons/search?nationality=QAT&pageSize=5");
        search.EnsureSuccessStatusCode();

        var recent = await client.GetFromJsonAsync<List<AuditRow>>("/api/v1/audit/recent?take=200", JsonOptions) ?? [];
        var entry = recent.FirstOrDefault(a => a.Action == "SEARCH" && a.Criteria != null && a.Criteria.Contains("nat=QAT"));

        Assert.NotNull(entry);
        Assert.Equal("operator1", entry!.Actor);
    }
}
