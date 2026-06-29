using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nrs.Infrastructure.Persistence;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Nrs.Infrastructure.Migrations.Oracle;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to generate the Oracle migration set into
/// this assembly. The connection string is unused by <c>migrations add</c> (generation is
/// offline); MigrationsAssembly points at this project so the migrations land here.
/// </summary>
public class OracleDesignTimeFactory : IDesignTimeDbContextFactory<NrsDbContext>
{
    public NrsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseOracle(
                "User Id=nrs_app;Password=design-time;Data Source=localhost:1521/XEPDB1",
                oracle =>
                {
                    oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
                    oracle.MigrationsAssembly(NrsDbContextFactory.OracleMigrationsAssembly);
                })
            .Options;

        return new NrsDbContext(options);
    }
}
