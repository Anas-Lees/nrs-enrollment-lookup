using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;

namespace Nrs.Application.Services;

/// <summary>
/// Cache-aside decorator over <see cref="IPersonLookupService"/>. In an operator console
/// the same profile is opened repeatedly, so profile reads (<see cref="GetByCrnAsync"/>)
/// are the hot path and are cached in a distributed cache — Redis in the deployed stack,
/// an in-memory cache otherwise.
///
/// IMPORTANT — audit safety: this decorator sits BELOW the controller's audit filter, so
/// every HTTP lookup is still recorded to the audit trail. A cache hit only skips the
/// database round-trip, never the "who looked up whom" record. Do not move caching up to
/// HTTP output-caching, which would short-circuit the controller and bypass the audit.
/// </summary>
public sealed class CachedPersonLookupService(
    IPersonLookupService inner,
    IDistributedCache cache) : IPersonLookupService
{
    private const string KeyPrefix = "person:";

    // Profiles change rarely (issuance/registration events), so a few minutes keeps reads
    // fresh enough while absorbing repeated opens of the same record.
    private static readonly DistributedCacheEntryOptions ProfileTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    /// <remarks>
    /// Search is intentionally pass-through: queries vary too widely to cache usefully,
    /// and fresh result counts matter more there than on a single profile.
    /// </remarks>
    public Task<PagedResult<PersonSummaryDto>> SearchAsync(
        PersonSearchCriteria criteria,
        CancellationToken cancellationToken = default)
        => inner.SearchAsync(criteria, cancellationToken);

    /// <inheritdoc />
    public async Task<PersonDto?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default)
    {
        var key = KeyPrefix + crn;

        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<PersonDto>(cached, Json);
        }

        var person = await inner.GetByCrnAsync(crn, cancellationToken);

        // Only cache hits, not misses: a CRN that doesn't exist yet may be enrolled later,
        // and we don't want a negative result pinned for the whole TTL.
        if (person is not null)
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(person, Json), ProfileTtl, cancellationToken);
        }

        return person;
    }
}
