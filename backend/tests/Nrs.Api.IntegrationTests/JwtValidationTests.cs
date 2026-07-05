using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Proves the hardened JwtBearer validation: only a correctly-signed token with the right
/// issuer, audience and an unexpired lifetime is accepted; everything else is 401.
/// </summary>
public class JwtValidationTests(NrsApiJwtFactory factory) : IClassFixture<NrsApiJwtFactory>
{
    private const string SearchUrl = "/api/v1/persons/search?pageSize=3";

    private static string Mint(string issuer, string audience, DateTime expires, params string[] realmRoles)
    {
        var creds = new SigningCredentials(NrsApiJwtFactory.SigningKey, SecurityAlgorithms.HmacSha256);
        var notBefore = DateTime.UtcNow.AddHours(-2);
        var payload = new JwtPayload(
            issuer,
            audience,
            [new Claim("preferred_username", "operator1"), new Claim("sub", "op-1")],
            notBefore,
            expires,
            notBefore);
        payload["realm_access"] = new Dictionary<string, object> { ["roles"] = realmRoles };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(creds), payload));
    }

    private HttpClient ClientWith(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [OracleFact]
    public async Task ValidOperatorToken_Returns200()
    {
        var token = Mint(NrsApiJwtFactory.Issuer, NrsApiJwtFactory.Audience, DateTime.UtcNow.AddHours(1), "operator");
        var response = await ClientWith(token).GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [OracleFact]
    public async Task WrongAudience_Returns401()
    {
        var token = Mint(NrsApiJwtFactory.Issuer, "some-other-api", DateTime.UtcNow.AddHours(1), "operator");
        var response = await ClientWith(token).GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [OracleFact]
    public async Task WrongIssuer_Returns401()
    {
        var token = Mint("https://evil-issuer/realms/nrs", NrsApiJwtFactory.Audience, DateTime.UtcNow.AddHours(1), "operator");
        var response = await ClientWith(token).GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [OracleFact]
    public async Task ExpiredToken_Returns401()
    {
        var token = Mint(NrsApiJwtFactory.Issuer, NrsApiJwtFactory.Audience, DateTime.UtcNow.AddHours(-1), "operator");
        var response = await ClientWith(token).GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [OracleFact]
    public async Task ValidTokenWithoutOperatorRole_Returns403()
    {
        var token = Mint(NrsApiJwtFactory.Issuer, NrsApiJwtFactory.Audience, DateTime.UtcNow.AddHours(1), "viewer");
        var response = await ClientWith(token).GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
