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

        // The cache is an accelerator, not a hard dependency: if Redis is unavailable, fall
        // through to the database rather than failing the request.
        var cached = await TryGetAsync(key, cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<PersonDto>(cached, Json);
        }

        var person = await inner.GetByCrnAsync(crn, cancellationToken);

        // Only cache hits, not misses: a CRN that doesn't exist yet may be enrolled later,
        // and we don't want a negative result pinned for the whole TTL.
        if (person is not null)
        {
            await TrySetAsync(key, JsonSerializer.Serialize(person, Json), cancellationToken);
        }

        return person;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Writes through to the inner service, then evicts the cached profile so the next read
    /// reflects the change immediately instead of serving the stale entry for the rest of its
    /// TTL. Eviction is best-effort — a cache that is momentarily unavailable simply expires
    /// the old value in a few minutes.
    /// </remarks>
    public async Task<PersonDto?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await inner.UpdateContactDetailsAsync(crn, request, cancellationToken);
        if (updated is not null)
        {
            await TryRemoveAsync(KeyPrefix + crn, cancellationToken);
        }

        return updated;
    }

    private async Task<string?> TryGetAsync(string key, CancellationToken ct)
    {
        try
        {
            return await cache.GetStringAsync(key, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null; // cache miss on any cache error
        }
    }

    private async Task TrySetAsync(string key, string value, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(key, value, ProfileTtl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Best-effort cache write; a failure to populate must not fail the lookup.
        }
    }

    private async Task TryRemoveAsync(string key, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Best-effort eviction; the stale entry expires on its own TTL if this fails.
        }
    }
}
