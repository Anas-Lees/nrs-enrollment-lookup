using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// EF Core execution strategy that retries transient Oracle connectivity failures
/// (the Oracle provider does not ship a built-in retrying strategy). Retries a few
/// times with backoff on common "server starting / connection lost / no listener"
/// ORA errors; non-transient errors surface immediately.
/// </summary>
public sealed class OracleTransientRetryStrategy : ExecutionStrategy
{
    // Common transient/connectivity ORA error numbers.
    private static readonly HashSet<int> TransientOraErrors =
    [
        3113, // end-of-file on communication channel
        3135, // connection lost contact
        12150, // unable to send data
        12153, // not connected
        12537, // connection closed
        12541, // no listener
        12514, // listener does not currently know of service
        1033, // ORACLE initialization or shutdown in progress
        1034, // ORACLE not available
        1089, // immediate shutdown in progress
    ];

    public OracleTransientRetryStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5))
    {
    }

    protected override bool ShouldRetryOn(Exception exception) =>
        exception is OracleException oracle && TransientOraErrors.Contains(oracle.Number)
        || exception is TimeoutException;
}
