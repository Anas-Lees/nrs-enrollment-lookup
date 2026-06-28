namespace Nrs.Application.Dtos;

/// <summary>
/// Generic pagination envelope returned by list/search endpoints.
/// </summary>
/// <typeparam name="T">The type of the items in the page.</typeparam>
public record PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; init; }

    /// <summary>The 1-based page number of this result.</summary>
    public int Page { get; init; }

    /// <summary>The number of items requested per page.</summary>
    public int PageSize { get; init; }
}
