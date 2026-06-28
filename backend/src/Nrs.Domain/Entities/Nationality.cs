namespace Nrs.Domain.Entities;

/// <summary>
/// Nationality reference/lookup. Persons link to it by ISO 3166-1 alpha-3 code, which
/// normalises nationality out of PERSON and provides bilingual display names.
/// Maps to the NATIONALITY table.
/// </summary>
public class Nationality
{
    /// <summary>ISO 3166-1 alpha-3 code. Primary key.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public ICollection<Person> Persons { get; set; } = [];
}
