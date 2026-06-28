namespace Nrs.Domain.Entities;

/// <summary>
/// A person's contact details (one-to-one with PERSON, sharing the CRN as its key).
/// Maps to the CONTACT table.
/// </summary>
public class Contact
{
    /// <summary>Civil Registration Number — primary key and FK to PERSON.</summary>
    public string CivilNumber { get; set; } = null!;

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public Person? Person { get; set; }
}
