using System.Net;
using Nrs.Api.Auth;

namespace Nrs.Api.IntegrationTests;

/// <summary>
/// Authorization tests: with auth enabled, the lookup endpoints require an authenticated
/// user who holds the Keycloak "operator" realm role. Authentication alone is not enough.
/// </summary>
public class AuthorizationTests(NrsApiAuthFactory factory) : IClassFixture<NrsApiAuthFactory>
{
    private const string SearchUrl = "/api/v1/persons/search?page=1&pageSize=5";

    [OracleFact]
    public async Task Search_WithoutAuthentication_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(SearchUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [OracleFact]
    public async Task Search_AuthenticatedButNotOperator_Returns403()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "viewer,auditor");

        var response = await client.GetAsync(SearchUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [OracleFact]
    public async Task Search_AsOperator_Returns200()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "operator");

        var response = await client.GetAsync(SearchUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [OracleFact]
    public async Task Health_IsReachable_WithoutAuthentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [OracleTheory]
    [InlineData("""{"roles":["operator","offline_access"]}""", new[] { "operator", "offline_access" })]
    [InlineData("""{"roles":[]}""", new string[0])]
    [InlineData("""{"other":"x"}""", new string[0])]
    [InlineData(null, new string[0])]
    [InlineData("not json", new string[0])]
    public void FromRealmAccess_ExtractsRoles(string? json, string[] expected)
    {
        var roles = KeycloakRoleClaims.FromRealmAccess(json);
        Assert.Equal(expected, roles);
    }
}
