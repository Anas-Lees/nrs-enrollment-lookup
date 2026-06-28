using Nrs.Application.Dtos;

namespace Nrs.Application.Interfaces;

/// <summary>
/// Application service contract for applicant lookup. Returns DTOs.
/// </summary>
public interface IPersonLookupService
{
    /// <summary>
    /// Searches persons matching the supplied criteria and returns a paged
    /// result of summary rows.
    /// </summary>
    /// <param name="criteria">The search filters and paging options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PagedResult<PersonSummaryDto>> SearchAsync(PersonSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full profile for a person by Civil Registration Number
    /// (CRN), or <see langword="null"/> if no such person exists.
    /// </summary>
    /// <param name="crn">The Civil Registration Number to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PersonDto?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default);
}
