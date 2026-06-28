namespace Nrs.Domain.Entities;

/// <summary>
/// A person's current residential address (one-to-one with PERSON, sharing the CRN as
/// its key). Maps to the ADDRESS table.
/// </summary>
public class Address
{
    /// <summary>Civil Registration Number — primary key and FK to PERSON.</summary>
    public string CivilNumber { get; set; } = null!;

    public string Governorate { get; set; } = null!;

    public string Wilayat { get; set; } = null!;

    public string? Village { get; set; }

    public string? Street { get; set; }

    public string? BuildingNumber { get; set; }

    public string? PostalCode { get; set; }

    public Person? Person { get; set; }
}
