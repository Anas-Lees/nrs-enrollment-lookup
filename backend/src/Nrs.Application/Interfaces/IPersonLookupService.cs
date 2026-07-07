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

    /// <summary>
    /// Sets a person's residential address and contact details, then returns their full,
    /// refreshed profile — or <see langword="null"/> if no person has the supplied CRN.
    /// Creates the address/contact rows if the person did not have them yet (the case for a
    /// freshly-registered applicant).
    /// </summary>
    /// <param name="crn">The Civil Registration Number of the person to update.</param>
    /// <param name="request">The new address and contact values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PersonDto?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sets a person's residential address alone, returning the refreshed profile (null if unknown CRN).</summary>
    Task<PersonDto?> UpdateAddressAsync(
        string crn,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sets a person's contact details alone, returning the refreshed profile (null if unknown CRN).</summary>
    Task<PersonDto?> UpdateContactAsync(
        string crn,
        UpdateContactRequest request,
        CancellationToken cancellationToken = default);
}
