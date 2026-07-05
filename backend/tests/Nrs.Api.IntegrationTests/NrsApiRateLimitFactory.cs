using Microsoft.AspNetCore.Hosting;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Like <see cref="NrsApiFactory"/> but with a deliberately tiny rate limit (3 per window)
/// so a test can trip the limiter quickly and assert a 429.
/// </summary>
public class NrsApiRateLimitFactory : OracleWebApplicationFactory
{
    protected override void ConfigureScenario(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("RateLimiting:PermitLimit", "3");
        builder.UseSetting("RateLimiting:WindowSeconds", "60");
    }
}
