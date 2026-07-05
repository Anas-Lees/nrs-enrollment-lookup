using Xunit;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// A <see cref="TheoryAttribute"/> that auto-skips when no Docker daemon is available, so the
/// Testcontainers-backed integration suite doesn't fail on a machine without Docker.
/// </summary>
public sealed class OracleTheoryAttribute : TheoryAttribute
{
    public OracleTheoryAttribute()
    {
        if (!OracleTestcontainer.IsDockerAvailable)
        {
            Skip = "Docker is not available; skipping the Oracle-backed integration test.";
        }
    }
}
