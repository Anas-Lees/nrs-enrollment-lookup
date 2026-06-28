namespace Nrs.Application.Dtos;

/// <summary>
/// Inbound search filters for an applicant lookup. All filters are optional;
/// paging has sensible defaults.
/// </summary>
public record PersonSearchCriteria
{
    /// <summary>Civil Registration Number (CRN) filter.</summary>
    public string? Crn { get; init; }

    /// <summary>Free-text name filter (Arabic or English).</summary>
    public string? Name { get; init; }

    /// <summary>Date of birth filter.</summary>
    public DateOnly? Dob { get; init; }

    /// <summary>ISO 3166-1 alpha-3 nationality code filter.</summary>
    public string? Nationality { get; init; }

    /// <summary>The 1-based page number to retrieve. Defaults to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>The number of items per page. Defaults to 20.</summary>
    public int PageSize { get; init; } = 20;
}
