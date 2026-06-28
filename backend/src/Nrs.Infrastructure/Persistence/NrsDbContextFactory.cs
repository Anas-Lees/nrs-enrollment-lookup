using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (e.g. <c>dotnet ef migrations</c>) can
/// construct the context without the application's DI host. Migrations for this POC
/// are generated against SQLite, the local-development provider (see ADR 0003).
/// </summary>
public class NrsDbContextFactory : IDesignTimeDbContextFactory<NrsDbContext>
{
    public NrsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseSqlite("Data Source=nrs-design.db")
            .Options;

        return new NrsDbContext(options);
    }
}
