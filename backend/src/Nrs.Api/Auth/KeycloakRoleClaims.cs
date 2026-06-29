using System.Text.Json;

namespace Nrs.Api.Auth;

/// <summary>
/// Keycloak puts realm roles inside a nested <c>realm_access.roles</c> claim, which
/// ASP.NET Core does not map to role claims automatically. This helper extracts those
/// role names so they can be added as standard role claims and checked by policies.
/// </summary>
public static class KeycloakRoleClaims
{
    /// <summary>The Keycloak claim holding realm-level access ({"roles":[...]}).</summary>
    public const string RealmAccessClaim = "realm_access";

    /// <summary>
    /// Parses the JSON value of a <c>realm_access</c> claim and returns its role names.
    /// Returns an empty sequence for null/blank/malformed input (fail safe, never throws).
    /// </summary>
    public static IReadOnlyList<string> FromRealmAccess(string? realmAccessJson)
    {
        if (string.IsNullOrWhiteSpace(realmAccessJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(realmAccessJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("roles", out var roles) ||
                roles.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<string>();
            foreach (var role in roles.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String)
                {
                    var name = role.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result.Add(name);
                    }
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
