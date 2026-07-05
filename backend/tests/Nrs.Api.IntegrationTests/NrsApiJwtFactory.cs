using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Auth enabled with the REAL JwtBearer scheme, but validating tokens signed by a known
/// test key (no live Keycloak). Issuer/audience/lifetime/signature are all enforced — so
/// tests can prove that wrong-issuer, wrong-audience and expired tokens are rejected.
/// </summary>
public class NrsApiJwtFactory : OracleWebApplicationFactory
{
    public const string Issuer = "https://test-issuer/realms/nrs";
    public const string Audience = "nrs-api";

    // 32+ bytes for HMAC-SHA256.
    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("nrs-test-signing-key-please-use-32+chars!!"));

    protected override void ConfigureScenario(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth:Enabled", "true");
        builder.UseSetting("Auth:Audience", Audience);
        builder.UseSetting("Auth:Authority", Issuer); // satisfies fail-fast; discovery is overridden below
        builder.UseSetting("Auth:RequireHttpsMetadata", "false");

        builder.ConfigureServices(services =>
        {
            // Validate against the test signing key/issuer instead of live OIDC discovery,
            // keeping the hardened ValidateAudience/Lifetime settings from the app.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null!; // force use of the static TokenValidationParameters below
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
            });
        });
    }
}
