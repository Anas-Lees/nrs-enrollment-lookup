using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (e.g. <c>dotnet ef migrations</c>) can construct
/// the context without the application's DI host. This one targets SQLite, the local-dev
/// provider, whose migrations live in this assembly. Oracle has its own design-time factory
/// and migration set in <see cref="OracleMigrationsAssembly"/> (separate assembly, since a
/// context can only load one migration set per assembly).
/// </summary>
public class NrsDbContextFactory : IDesignTimeDbContextFactory<NrsDbContext>
{
    /// <summary>Assembly that holds the Oracle-specific migration set (used at runtime).</summary>
    public const string OracleMigrationsAssembly = "Nrs.Infrastructure.Migrations.Oracle";

    public NrsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseSqlite("Data Source=nrs-design.db")
            .Options;

        return new NrsDbContext(options);
    }
}
