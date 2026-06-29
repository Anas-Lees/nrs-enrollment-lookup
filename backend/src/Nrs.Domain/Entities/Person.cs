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

    /// <summary>
    /// Normalized, searchable concatenation of all four name parts (see NameNormalizer):
    /// diacritics/tatweel stripped, alef/yaa/taa-marbuta/hamza folded, accents removed,
    /// lower-cased. Populated on write; matched (LIKE) for fuzzy bilingual name search.
    /// Nullable: Oracle stores an empty string as NULL, and a nullable column also adds
    /// cleanly to an existing table (the seeder then backfills it).
    /// </summary>
    public string? NameSearch { get; set; }

    public DateOnly DateOfBirth { get; set; }

    /// <summary>Single character: "M" or "F".</summary>
    public string Gender { get; set; } = null!;

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; set; } = null!;

    public PersonStatus Status { get; set; }

    public string? PhotoPath { get; set; }

    // --- Extended biographic data ---

    public string? PlaceOfBirthEn { get; set; }

    public string? PlaceOfBirthAr { get; set; }

    public string? MotherNameEn { get; set; }

    public string? MotherNameAr { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    /// <summary>ABO/Rh blood group, e.g. "O+".</summary>
    public string? BloodType { get; set; }

    public string? OccupationEn { get; set; }

    public string? OccupationAr { get; set; }

    // --- Relationships ---

    /// <summary>Nationality reference (by <see cref="NationalityCode"/>).</summary>
    public Nationality? Nationality { get; set; }

    /// <summary>Current residential address (one-to-one).</summary>
    public Address? Address { get; set; }

    /// <summary>Contact details (one-to-one).</summary>
    public Contact? Contact { get; set; }

    public ICollection<IdCard> IdCards { get; set; } = [];

    public ICollection<Passport> Passports { get; set; } = [];
}
