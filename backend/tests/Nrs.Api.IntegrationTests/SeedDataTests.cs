using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the startup seed directly against the EF Core context resolved from the
/// running application's DI container (the same in-memory SQLite database the API uses).
/// </summary>
public class SeedDataTests : IClassFixture<NrsApiFactory>
{
    private readonly NrsApiFactory _factory;

    public SeedDataTests(NrsApiFactory factory)
    {
        // Touch the client so the host is built and startup (migrate + seed) has run.
        _ = factory.CreateClient();
        _factory = factory;
    }

    [Fact]
    public async Task Seed_HasAtLeast100Persons()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();

        var count = await db.Persons.CountAsync();

        Assert.True(count >= 100, $"Expected at least 100 seeded persons but found {count}.");
    }

    [Fact]
    public async Task Seed_EveryPersonHasIdCardAndPassport()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NrsDbContext>();

        var withoutIdCard = await db.Persons.CountAsync(p => !p.IdCards.Any());
        var withoutPassport = await db.Persons.CountAsync(p => !p.Passports.Any());

        Assert.Equal(0, withoutIdCard);
        Assert.Equal(0, withoutPassport);
    }
}
