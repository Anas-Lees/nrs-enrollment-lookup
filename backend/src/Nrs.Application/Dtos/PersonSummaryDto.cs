using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>
/// A single row in the applicant search results table.
/// </summary>
public record PersonSummaryDto
{
    /// <summary>Civil Registration Number (CRN). Identifies the person.</summary>
    public string CivilNumber { get; init; } = null!;

    /// <summary>First name in Arabic.</summary>
    public string FirstNameAr { get; init; } = null!;

    /// <summary>Family name in Arabic.</summary>
    public string FamilyNameAr { get; init; } = null!;

    /// <summary>First name in English.</summary>
    public string FirstNameEn { get; init; } = null!;

    /// <summary>Family name in English.</summary>
    public string FamilyNameEn { get; init; } = null!;

    /// <summary>Date of birth.</summary>
    public DateOnly DateOfBirth { get; init; }

    /// <summary>Single character: "M" or "F".</summary>
    public string Gender { get; init; } = null!;

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; init; } = null!;

    /// <summary>English nationality name, resolved from the lookup (null if unmatched).</summary>
    public string? NationalityNameEn { get; init; }

    /// <summary>Arabic nationality name, resolved from the lookup (null if unmatched).</summary>
    public string? NationalityNameAr { get; init; }

    /// <summary>Lifecycle status of the person record.</summary>
    public PersonStatus Status { get; init; }
}
