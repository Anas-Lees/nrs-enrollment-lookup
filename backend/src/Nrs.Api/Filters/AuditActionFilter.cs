using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Domain.Enums;

namespace Nrs.Api.Filters;

/// <summary>
/// Writes an append-only audit record for every applicant-lookup action: who (operator
/// or "anonymous"), what (search/profile), against whom (target CRN), with what filters,
/// how many results, and from which IP. Runs after the action so it can read the result;
/// audit failures fail the request (no un-audited disclosure).
/// </summary>
public class AuditActionFilter(IAuditLogger auditLogger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var actionName = (context.ActionDescriptor as ControllerActionDescriptor)?.ActionName;
        var http = context.HttpContext;
        var actor =
            http.User.FindFirst("preferred_username")?.Value
            ?? http.User.Identity?.Name
            ?? "anonymous";
        var ip = http.Connection.RemoteIpAddress?.ToString();

        // Use a non-cancellable token so the audit record is written even if the caller
        // disconnects after the data was produced.
        if (actionName == "Search")
        {
            var criteria = context.ActionArguments.Values.OfType<PersonSearchCriteria>().FirstOrDefault();
            var count = (executed.Result as ObjectResult)?.Value is PagedResult<PersonSummaryDto> page
                ? page.TotalCount
                : (int?)null;
            await auditLogger.LogAsync(
                actor, ip, AuditAction.SEARCH, targetCrn: null, Summarize(criteria), count, CancellationToken.None);
        }
        else if (actionName == "GetByCrn")
        {
            var crn = context.ActionArguments.TryGetValue("crn", out var value) ? value?.ToString() : null;
            await auditLogger.LogAsync(
                actor, ip, AuditAction.VIEW_PROFILE, crn, criteria: null, resultCount: null, CancellationToken.None);
        }
    }

    private static string Summarize(PersonSearchCriteria? c)
    {
        if (c is null)
        {
            return "(all)";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Crn)) parts.Add($"crn={c.Crn}");
        if (!string.IsNullOrWhiteSpace(c.Name)) parts.Add($"name={c.Name}");
        if (c.Dob is { } dob) parts.Add($"dob={dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(c.Nationality)) parts.Add($"nat={c.Nationality}");
        return parts.Count == 0 ? "(all)" : string.Join(", ", parts);
    }
}
