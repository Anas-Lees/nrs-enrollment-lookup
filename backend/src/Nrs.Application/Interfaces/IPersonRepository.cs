using Nrs.Application.Dtos;
using Nrs.Domain.Entities;

namespace Nrs.Application.Interfaces;

/// <summary>
/// Data-access contract for persons. Implemented in the Infrastructure layer.
/// Returns Domain entities; the service maps them to DTOs.
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Searches persons matching the supplied criteria, returning the page of
    /// matching entities together with the total count of all matches.
    /// </summary>
    /// <param name="criteria">The search filters and paging options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<(IReadOnlyList<Person> Items, int TotalCount)> SearchAsync(PersonSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single person by Civil Registration Number (CRN), or
    /// <see langword="null"/> if no such person exists.
    /// </summary>
    /// <param name="crn">The Civil Registration Number to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<Person?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the supplied address and contact details to the person with the given CRN,
    /// creating the address/contact rows if they do not exist yet, and returns the refreshed
    /// person (with related data) — or <see langword="null"/> if no such person exists.
    /// </summary>
    /// <param name="crn">The Civil Registration Number of the person to update.</param>
    /// <param name="request">The new address and contact values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<Person?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default);
}
