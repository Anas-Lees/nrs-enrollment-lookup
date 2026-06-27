using Nrs.Domain.Enums;

namespace Nrs.Domain.Entities;

/// <summary>
/// A person/applicant. Hub entity. Maps to the PERSON table.
/// </summary>
public class Person
{
    /// <summary>Civil Registration Number (CRN). Primary key.</summary>
    public string CivilNumber { get; set; } = null!;

    public string FirstNameAr { get; set; } = null!;

    public string FamilyNameAr { get; set; } = null!;

    public string FirstNameEn { get; set; } = null!;

    public string FamilyNameEn { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    /// <summary>Single character: "M" or "F".</summary>
    public string Gender { get; set; } = null!;

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; set; } = null!;

    public PersonStatus Status { get; set; }

    public string? PhotoPath { get; set; }

    public ICollection<IdCard> IdCards { get; set; } = [];

    public ICollection<Passport> Passports { get; set; } = [];
}
