using Nrs.Application.Search;

namespace Nrs.Application.Tests;

public class NameNormalizerTests
{
    [Theory]
    // Alef forms all fold to bare alef.
    [InlineData("أحمد", "احمد")]
    [InlineData("إبراهيم", "ابراهيم")]
    [InlineData("آدم", "ادم")]
    // Tashkeel (diacritics) are stripped.
    [InlineData("مُحَمَّد", "محمد")]
    // Tatweel (kashida) elongation is removed.
    [InlineData("محـمـد", "محمد")]
    // Taa-marbuta → haa.
    [InlineData("فاطمة", "فاطمه")]
    // Alef-maksura → yaa.
    [InlineData("يحيى", "يحيي")]
    // Hamza carriers fold to their base letter.
    [InlineData("مؤمن", "مومن")]
    [InlineData("بائع", "بايع")]
    public void Normalize_FoldsArabicOrthographicVariants(string input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_MakesArabicVariantsCompareEqual()
    {
        // The whole point: differently-written forms of the same name normalise identically.
        Assert.Equal(NameNormalizer.Normalize("أَحْمَد"), NameNormalizer.Normalize("احمد"));
        Assert.Equal(NameNormalizer.Normalize("مُحَمّد"), NameNormalizer.Normalize("محمد"));
    }

    [Theory]
    [InlineData("AHMED", "ahmed")]
    [InlineData("José", "jose")]
    [InlineData("Müller", "muller")]
    [InlineData("  Al   Balushi  ", "al balushi")]
    public void Normalize_LowercasesStripsLatinAccentsAndCollapsesSpace(string input, string expected)
    {
        Assert.Equal(expected, NameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsEmpty_ForBlank(string? input)
    {
        Assert.Equal(string.Empty, NameNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var once = NameNormalizer.Normalize("أحمد الـبلوشي");
        Assert.Equal(once, NameNormalizer.Normalize(once));
    }
}
