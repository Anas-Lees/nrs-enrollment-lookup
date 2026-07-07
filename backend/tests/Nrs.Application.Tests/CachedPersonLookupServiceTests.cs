using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Application.Services;

namespace Nrs.Application.Tests;

/// <summary>
/// The cache-aside decorator serves repeated profile reads from the distributed cache.
/// (That this never bypasses the audit trail is asserted end-to-end in the integration
/// tests, since the audit filter runs above this layer.)
/// </summary>
public class CachedPersonLookupServiceTests
{
    [Fact]
    public async Task GetByCrnAsync_SecondCall_ServedFromCache_WithoutHittingInner()
    {
        var inner = new CountingLookupService { Result = BuildDto("44444444") };
        var service = new CachedPersonLookupService(inner, NewCache());

        var first = await service.GetByCrnAsync("44444444");
        var second = await service.GetByCrnAsync("44444444");

        Assert.Equal(1, inner.GetByCrnCalls); // first populated the cache; second was a hit
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("44444444", second!.CivilNumber);
        Assert.Equal(first!.FirstNameEn, second.FirstNameEn);
    }

    [Fact]
    public async Task GetByCrnAsync_DoesNotCacheMisses()
    {
        var inner = new CountingLookupService { Result = null };
        var service = new CachedPersonLookupService(inner, NewCache());

        await service.GetByCrnAsync("00000000");
        await service.GetByCrnAsync("00000000");

        // A not-found result is not cached (the CRN may be enrolled later), so the inner
        // service is consulted every time.
        Assert.Equal(2, inner.GetByCrnCalls);
    }

    [Fact]
    public async Task GetByCrnAsync_CachesPerCrn()
    {
        var inner = new CountingLookupService { Result = BuildDto("55555555") };
        var service = new CachedPersonLookupService(inner, NewCache());

        await service.GetByCrnAsync("55555555");
        inner.Result = BuildDto("66666666");
        await service.GetByCrnAsync("66666666");
        await service.GetByCrnAsync("55555555"); // still cached separately

        Assert.Equal(2, inner.GetByCrnCalls); // one per distinct CRN, third was a hit
    }

    [Fact]
    public async Task SearchAsync_IsPassThrough()
    {
        var inner = new CountingLookupService();
        var service = new CachedPersonLookupService(inner, NewCache());

        await service.SearchAsync(new PersonSearchCriteria());
        await service.SearchAsync(new PersonSearchCriteria());

        Assert.Equal(2, inner.SearchCalls);
    }

    [Fact]
    public async Task UpdateContactDetailsAsync_EvictsCache_SoNextReadIsFresh()
    {
        var inner = new CountingLookupService { Result = BuildDto("77777777") };
        var service = new CachedPersonLookupService(inner, NewCache());

        await service.GetByCrnAsync("77777777"); // populates the cache (inner call #1)
        await service.UpdateContactDetailsAsync("77777777", new UpdateContactDetailsRequest
        {
            Governorate = "Muscat",
            Wilayat = "Seeb",
        });
        await service.GetByCrnAsync("77777777"); // cache was evicted → inner call #2

        Assert.Equal(2, inner.GetByCrnCalls);
    }

    // --- helpers ---------------------------------------------------------

    private static MemoryDistributedCache NewCache()
        => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static PersonDto BuildDto(string crn) => new()
    {
        CivilNumber = crn,
        FirstNameAr = "الاسم",
        FamilyNameAr = "العائلة",
        FirstNameEn = "First",
        FamilyNameEn = "Family",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Gender = "M",
        NationalityCode = "OMN",
    };

    private sealed class CountingLookupService : IPersonLookupService
    {
        public int GetByCrnCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public int UpdateContactCalls { get; private set; }
        public PersonDto? Result { get; set; }

        public Task<PagedResult<PersonSummaryDto>> SearchAsync(
            PersonSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(new PagedResult<PersonSummaryDto>());
        }

        public Task<PersonDto?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default)
        {
            GetByCrnCalls++;
            return Task.FromResult(Result);
        }

        public Task<PersonDto?> UpdateContactDetailsAsync(
            string crn, UpdateContactDetailsRequest request, CancellationToken cancellationToken = default)
        {
            UpdateContactCalls++;
            return Task.FromResult(Result);
        }
    }
}
