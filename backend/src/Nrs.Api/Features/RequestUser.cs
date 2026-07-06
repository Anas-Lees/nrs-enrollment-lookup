using System.Security.Claims;

namespace Nrs.Api.Features;

/// <summary>
/// Reads the calling staff member's identity off the request. When auth is disabled (the
/// local POC posture) there is no principal, so the caller is "anonymous" and — for demo
/// usability — holds every role, letting one person walk the whole operator/reviewer/
/// supervisor journey without logging in three times.
/// </summary>
public static class RequestUser
{
    public const string OperatorRole = "operator";
    public const string ReviewerRole = "reviewer";
    public const string SupervisorRole = "supervisor";

    private static readonly string[] AllRoles = [OperatorRole, ReviewerRole, SupervisorRole];

    /// <summary>Authenticated username, or "anonymous" when auth is off.</summary>
    public static string Username(HttpContext http) =>
        http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";

    /// <summary>The user's realm roles; every role when auth is off.</summary>
    public static IReadOnlyList<string> Roles(HttpContext http) =>
        http.User.Identity?.IsAuthenticated == true
            ? http.User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList()
            : AllRoles;
}
