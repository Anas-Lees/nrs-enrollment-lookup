using System.Globalization;
using System.Text;

namespace Nrs.Application.Search;

/// <summary>
/// Normalises a person name so search is insensitive to the common orthographic variants
/// that otherwise make Arabic (and transliterated) names hard to match:
/// <list type="bullet">
///   <item>Arabic diacritics (tashkeel) and the tatweel elongation are removed.</item>
///   <item>Alef forms (أ إ آ ٱ) fold to bare alef (ا); alef-maksura (ى) → yaa (ي);
///         taa-marbuta (ة) → haa (ه); hamza-carriers (ؤ ئ) fold to و / ي; bare hamza (ء) drops.</item>
///   <item>Latin accents are stripped (e.g. José → jose) and text is lower-cased.</item>
///   <item>Whitespace is trimmed and collapsed.</item>
/// </list>
/// Both the stored name and the query are normalised the same way, so "أحمد", "احمد" and
/// "أَحْمَد" all compare equal. This is deterministic and provider-independent (it runs in C#,
/// populating persisted normalized columns), unlike DB-specific fuzzy/edit-distance functions.
/// </summary>
public static class NameNormalizer
{
    /// <summary>Returns the normalized form, or an empty string for null/blank input.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Decompose (NFD) so combining marks separate from their base letters — this covers
        // both Latin accents and Arabic tashkeel, and splits أ/إ/آ/ؤ/ئ into base + hamza.
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true; // start true so leading whitespace is dropped

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue; // drop accents / tashkeel / decomposed hamza
            }

            if (ch == 'ـ')
            {
                continue; // tatweel (kashida) elongation
            }

            var mapped = ch switch
            {
                'ى' => 'ي', // alef-maksura  ى → yaa ي
                'ٱ' => 'ا', // alef-wasla    ٱ → alef ا
                'ة' => 'ه', // taa-marbuta   ة → haa ه
                'ء' => '\0', // bare hamza    ء → drop
                _ => ch,
            };

            if (mapped == '\0')
            {
                continue;
            }

            if (char.IsWhiteSpace(mapped))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(char.ToLowerInvariant(mapped));
                lastWasSpace = false;
            }
        }

        // Re-compose to NFC (base letters only now) and trim any trailing space.
        return sb.ToString().TrimEnd().Normalize(NormalizationForm.FormC);
    }
}
