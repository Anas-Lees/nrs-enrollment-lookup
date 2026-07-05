using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies fuzzy bilingual name search end-to-end: the normalized NameSearch column makes
/// matching insensitive to Arabic diacritics and to English case. Self-referential — it
/// reads a real seeded person and searches by variants of that person's own name, so it
/// does not depend on any specific name being present. (The exact orthographic folding
/// rules — alef/hamza/taa-marbuta/maksura — are covered by NameNormalizerTests.)
/// </summary>
public class SearchFuzzyTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonElement> SearchAsync(string name)
    {
        var url = $"/api/v1/persons/search?name={Uri.EscapeDataString(name)}&pageSize=100";
        return await _client.GetFromJsonAsync<JsonElement>(url);
    }

    private async Task<(string Crn, string FirstNameEn, string FirstNameAr)> AnyPersonAsync()
    {
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/v1/persons/search?pageSize=1");
        var p = page.GetProperty("items")[0];
        return (
            p.GetProperty("civilNumber").GetString()!,
            p.GetProperty("firstNameEn").GetString()!,
            p.GetProperty("firstNameAr").GetString()!);
    }

    private static bool ContainsCrn(JsonElement page, string crn) =>
        page.GetProperty("items").EnumerateArray()
            .Any(i => i.GetProperty("civilNumber").GetString() == crn);

    [OracleFact]
    public async Task EnglishSearch_IsCaseInsensitive()
    {
        var person = await AnyPersonAsync();

        Assert.True(ContainsCrn(await SearchAsync(person.FirstNameEn.ToUpperInvariant()), person.Crn));
        Assert.True(ContainsCrn(await SearchAsync(person.FirstNameEn.ToLowerInvariant()), person.Crn));
    }

    [OracleFact]
    public async Task ArabicSearch_IsDiacriticInsensitive()
    {
        var person = await AnyPersonAsync();

        // The plain Arabic name finds the person...
        Assert.True(ContainsCrn(await SearchAsync(person.FirstNameAr), person.Crn));

        // ...and so does the same name with a tashkeel diacritic (fatha) inserted, because
        // both the stored column and the query are normalized identically.
        var diacritized = person.FirstNameAr.Insert(1, "َ");
        Assert.True(ContainsCrn(await SearchAsync(diacritized), person.Crn));
    }

    [OracleFact]
    public async Task Search_TrimsAndMatches()
    {
        var person = await AnyPersonAsync();

        // Surrounding whitespace is normalized away.
        Assert.True(ContainsCrn(await SearchAsync($"  {person.FirstNameEn}  "), person.Crn));
    }
}
