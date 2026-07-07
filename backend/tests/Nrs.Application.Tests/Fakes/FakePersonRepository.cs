using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Domain.Entities;

namespace Nrs.Application.Tests;

/// <summary>
/// Hand-written <see cref="IPersonRepository"/> test double (no mocking library).
/// Returned values are configured via properties; the criteria passed to
/// <see cref="SearchAsync"/> is recorded in <see cref="LastCriteria"/> so tests
/// can assert that the service forwarded the normalised paging values.
/// </summary>
internal sealed class FakePersonRepository : IPersonRepository
{
    public IReadOnlyList<Person> SearchResult { get; set; } = [];

    public int TotalCount { get; set; }

    public Person? GetByCrnResult { get; set; }

    public Person? UpdateContactResult { get; set; }

    public UpdateContactDetailsRequest? LastContactRequest { get; private set; }

    public PersonSearchCriteria? LastCriteria { get; private set; }

    public Task<(IReadOnlyList<Person> Items, int TotalCount)> SearchAsync(
        PersonSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastCriteria = criteria;
        return Task.FromResult((SearchResult, TotalCount));
    }

    public Task<Person?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default)
        => Task.FromResult(GetByCrnResult);

    public Task<Person?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        LastContactRequest = request;
        return Task.FromResult(UpdateContactResult);
    }
}
