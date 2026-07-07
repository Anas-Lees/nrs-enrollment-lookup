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

    /// <inheritdoc />
    public async Task<PersonDto?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var person = await repository.UpdateContactDetailsAsync(crn, Trim(request), cancellationToken);
        return person?.ToDto();
    }

    // Trim free-text input and fold blank optionals to null so the stored record is clean
    // (Oracle also treats an empty string as NULL, so this keeps C# and the database aligned).
    private static UpdateContactDetailsRequest Trim(UpdateContactDetailsRequest r) => r with
    {
        Governorate = r.Governorate.Trim(),
        Wilayat = r.Wilayat.Trim(),
        Village = Blank(r.Village),
        Street = Blank(r.Street),
        BuildingNumber = Blank(r.BuildingNumber),
        PostalCode = Blank(r.PostalCode),
        Mobile = Blank(r.Mobile),
        Email = Blank(r.Email),
    };

    private static string? Blank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    // Clamp paging so the response envelope reports the values actually used.
    private static PersonSearchCriteria Normalise(PersonSearchCriteria criteria) => criteria with
    {
        Page = criteria.Page < 1 ? 1 : criteria.Page,
        PageSize = criteria.PageSize is < 1 or > 100 ? 20 : criteria.PageSize,
    };
}
