using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>
/// Full applicant profile, including related documents. Extends the summary row.
/// </summary>
public record PersonDto : PersonSummaryDto
{
    /// <summary>Relative path to the person's photo, if available.</summary>
    public string? PhotoPath { get; init; }

    /// <summary>Place of birth, English.</summary>
    public string? PlaceOfBirthEn { get; init; }

    /// <summary>Place of birth, Arabic.</summary>
    public string? PlaceOfBirthAr { get; init; }

    /// <summary>Mother's name, English.</summary>
    public string? MotherNameEn { get; init; }

    /// <summary>Mother's name, Arabic.</summary>
    public string? MotherNameAr { get; init; }

    /// <summary>Marital status.</summary>
    public MaritalStatus? MaritalStatus { get; init; }

    /// <summary>ABO/Rh blood group, e.g. "O+".</summary>
    public string? BloodType { get; init; }

    /// <summary>Occupation, English.</summary>
    public string? OccupationEn { get; init; }

    /// <summary>Occupation, Arabic.</summary>
    public string? OccupationAr { get; init; }

    /// <summary>Current residential address.</summary>
    public AddressDto? Address { get; init; }

    /// <summary>Contact details.</summary>
    public ContactDto? Contact { get; init; }

    /// <summary>ID cards associated with the person.</summary>
    public IReadOnlyList<IdCardDto> IdCards { get; init; } = [];

    /// <summary>Passports associated with the person.</summary>
    public IReadOnlyList<PassportDto> Passports { get; init; } = [];
}
