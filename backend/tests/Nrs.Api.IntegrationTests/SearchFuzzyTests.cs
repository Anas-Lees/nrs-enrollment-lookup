using System.Net.Http.Json;
using System.Text.Json;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies fuzzy bilingual name search end-to-end: the normalized NameSearch column makes
/// matching insensitive to Arabic orthographic variants (diacritics, alef/hamza forms) and
/// to English case. Tolerant of the exact seeded set — asserts equivalence between variant
/// spellings of the same query rather than fixed counts.
/// </summary>
public class SearchFuzzyTests(NrsApiFactory factory) : IClassFixture<NrsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<int> CountAsync(string name)
    {
        var url = $"/api/v1/persons/search?name={Uri.EscapeDataString(name)}&pageSize=100";
        var page = await _client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task ArabicSearch_IsDiacriticInsensitive()
    {
        // "محمد" plain vs. with tashkeel (shadda/fatha) must match the same people.
        var plain = await CountAsync("محمد");
        var diacritized = await CountAsync("مُحَمَّد");

        Assert.True(plain > 0, "expected at least one 'محمد' in the seeded data");
        Assert.Equal(plain, diacritized);
    }

    [Fact]
    public async Task ArabicSearch_FoldsAlefAndHamzaForms()
    {
        // "أحمد" (alef-hamza) and "احمد" (bare alef) are the same name.
        var withHamza = await CountAsync("أحمد");
        var bareAlef = await CountAsync("احمد");

        Assert.True(bareAlef > 0, "expected at least one 'احمد' in the seeded data");
        Assert.Equal(withHamza, bareAlef);
    }

    [Fact]
    public async Task EnglishSearch_IsCaseInsensitive()
    {
        var lower = await CountAsync("mohammed");
        var upper = await CountAsync("MOHAMMED");
        var mixed = await CountAsync("MoHammed");

        Assert.True(lower > 0, "expected at least one 'Mohammed' in the seeded data");
        Assert.Equal(lower, upper);
        Assert.Equal(lower, mixed);
    }

    [Fact]
    public async Task Search_MatchesEitherFirstOrFamilyName()
    {
        // A common Omani family name still matches via the combined NameSearch column.
        var byFamily = await CountAsync("Balushi");
        Assert.True(byFamily > 0, "expected at least one 'Al-Balushi' in the seeded data");
    }
}
