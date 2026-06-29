using System.ComponentModel.DataAnnotations;

namespace Nrs.Application.Dtos;

/// <summary>
/// Inbound search filters for an applicant lookup. All filters are optional;
/// paging has sensible defaults. Validated at the API boundary (malformed input is
/// rejected with 400) — see the data annotations below.
/// </summary>
public record PersonSearchCriteria
{
    /// <summary>Civil Registration Number (CRN) filter. Digits only; a prefix is allowed.</summary>
    [RegularExpression(@"^\d{1,9}$", ErrorMessage = "CRN must be 1 to 9 digits.")]
    public string? Crn { get; init; }

    /// <summary>Free-text name filter (Arabic or English).</summary>
    [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string? Name { get; init; }

    /// <summary>Date of birth filter.</summary>
    public DateOnly? Dob { get; init; }

    /// <summary>ISO 3166-1 alpha-3 nationality code filter.</summary>
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Nationality must be a 3-letter ISO code.")]
    public string? Nationality { get; init; }

    /// <summary>The 1-based page number to retrieve. Defaults to 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    /// <summary>The number of items per page (1–100). Defaults to 20.</summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; init; } = 20;
}
