using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Application.Mapping;

namespace Nrs.Application.Services;

/// <summary>
/// Orchestrates applicant lookup: normalises the request, delegates data access to the
/// repository, and maps the resulting entities to DTOs. Holds no EF Core concerns.
/// </summary>
public class PersonLookupService(IPersonRepository repository) : IPersonLookupService
{
    /// <inheritdoc />
    public async Task<PagedResult<PersonSummaryDto>> SearchAsync(
        PersonSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(criteria);
        var (items, totalCount) = await repository.SearchAsync(normalised, cancellationToken);

        return new PagedResult<PersonSummaryDto>
        {
            Items = items.Select(person => person.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = normalised.Page,
            PageSize = normalised.PageSize,
        };
    }

    /// <inheritdoc />
    public async Task<PersonDto?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default)
    {
        var person = await repository.GetByCrnAsync(crn, cancellationToken);
        return person?.ToDto();
    }

    // Clamp paging so the response envelope reports the values actually used.
    private static PersonSearchCriteria Normalise(PersonSearchCriteria criteria) => criteria with
    {
        Page = criteria.Page < 1 ? 1 : criteria.Page,
        PageSize = criteria.PageSize is < 1 or > 100 ? 20 : criteria.PageSize,
    };
}
