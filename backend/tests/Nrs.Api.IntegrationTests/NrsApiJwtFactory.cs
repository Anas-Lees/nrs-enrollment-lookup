using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Auth enabled with the REAL JwtBearer scheme, but validating tokens signed by a known
/// test key (no live Keycloak). Issuer/audience/lifetime/signature are all enforced — so
/// tests can prove that wrong-issuer, wrong-audience and expired tokens are rejected.
/// </summary>
public class NrsApiJwtFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://test-issuer/realms/nrs";
    public const string Audience = "nrs-api";

    // 32+ bytes for HMAC-SHA256.
    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("nrs-test-signing-key-please-use-32+chars!!"));

    private readonly SqliteConnection _connection;

    public NrsApiJwtFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth:Enabled", "true");
        builder.UseSetting("Auth:Audience", Audience);
        builder.UseSetting("Auth:Authority", Issuer); // satisfies fail-fast; discovery is overridden below
        builder.UseSetting("Auth:RequireHttpsMetadata", "false");

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NrsDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(NrsDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<NrsDbContext>(options => options.UseSqlite(_connection));

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
