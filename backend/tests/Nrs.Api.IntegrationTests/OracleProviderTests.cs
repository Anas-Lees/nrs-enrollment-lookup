using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Infrastructure.Persistence;
using Nrs.Infrastructure.Repositories;
using Nrs.Infrastructure.Seed;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Verifies the model and repository work against a real Oracle database, using the shared
/// Testcontainers Oracle container. Auto-skips when Docker is unavailable (see [OracleFact]).
/// </summary>
public class OracleProviderTests
{
    [OracleFact]
    public async Task Oracle_CreatesSchema_Seeds_AndSearches()
    {
        var connectionString = await OracleTestcontainer.GetConnectionStringAsync();
        await OracleTestcontainer.ResetSchemaAsync(connectionString);

        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseOracle(
                connectionString,
                o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;

        await using var db = new NrsDbContext(options);

        // Create the schema from the model (Arabic columns → NVARCHAR2 on Oracle) and seed.
        await db.Database.EnsureCreatedAsync();
        await DataSeeder.SeedAsync(db);

        // Seeded data is present.
        Assert.True(await db.Persons.CountAsync() >= 100);

        // Arabic round-trips through NVARCHAR2.
        var sample = await db.Persons.FirstAsync();
        Assert.False(string.IsNullOrWhiteSpace(sample.FirstNameAr));

        // The repository's filters (LOWER/LIKE) translate and run on Oracle.
        var repo = new PersonRepository(db);
        var (items, total) = await repo.SearchAsync(new PersonSearchCriteria { Nationality = "OMN" });
        Assert.True(total > 0);
        Assert.All(items, p => Assert.Equal("OMN", p.NationalityCode));

        // Profile + documents load.
        var profile = await repo.GetByCrnAsync(items[0].CivilNumber);
        Assert.NotNull(profile);
    }
}
