using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Nrs.Infrastructure.Persistence;
using Oracle.EntityFrameworkCore.Infrastructure;
using Testcontainers.Oracle;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Owns a single throwaway Oracle container shared by every integration-test factory. The
/// container starts on first use and is reaped when the test process exits (Testcontainers'
/// resource reaper). When Docker is unavailable the whole Oracle-backed suite auto-skips
/// (see <see cref="OracleFactAttribute"/>), so a checkout without Docker still runs green.
/// </summary>
public static class OracleTestcontainer
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static OracleContainer? _container;

    /// <summary>True when a Docker daemon is reachable. Probed once, at discovery time.</summary>
    public static bool IsDockerAvailable => DockerProbe.Value;

    private static readonly Lazy<bool> DockerProbe = new(() =>
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(20_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    });

    /// <summary>Starts the shared container (once) and returns its connection string.</summary>
    public static async Task<string> GetConnectionStringAsync()
    {
        if (_container is not null)
        {
            return _container.GetConnectionString();
        }

        await Gate.WaitAsync();
        try
        {
            if (_container is null)
            {
                // Reuse the same image the compose stack uses, so it's already cached locally.
                var container = new OracleBuilder()
                    .WithImage("gvenzl/oracle-xe:21-slim")
                    .Build();
                await container.StartAsync();
                _container = container;
            }

            return _container.GetConnectionString();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Drops everything in the shared schema so the next test class starts from a clean slate;
    /// the app's own startup then migrates and (per its config) seeds it — mirroring the
    /// per-class isolation the old in-memory SQLite databases used to provide.
    /// </summary>
    public static async Task ResetSchemaAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<NrsDbContext>()
            .UseOracle(connectionString, o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;

        await using var db = new NrsDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}
