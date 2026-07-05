using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (e.g. <c>dotnet ef migrations</c>) can construct
/// the context without the application's DI host. The connection string is only a
/// placeholder — migration generation is offline — and the single Oracle migration set
/// lives in this same assembly.
/// </summary>
public class NrsDbContextFactory : IDesignTimeDbContextFactory<NrsDbContext>
{
    public NrsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseOracle(
                "User Id=nrs_app;Password=design-time;Data Source=localhost:1521/XEPDB1",
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;

        return new NrsDbContext(options);
    }
}
