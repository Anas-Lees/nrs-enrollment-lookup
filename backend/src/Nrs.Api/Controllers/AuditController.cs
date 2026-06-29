using Microsoft.AspNetCore.Mvc;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;

namespace Nrs.Api.Controllers;

/// <summary>
/// Read access to the lookup audit trail (recent lookups). When auth is enabled this is
/// operator-only via the fallback policy, like the lookup endpoints.
/// </summary>
[ApiController]
[Route("api/v1/audit")]
[Produces("application/json")]
public class AuditController(IAuditLogger auditLogger) : ControllerBase
{
    /// <summary>Returns the most recent audit records (newest first).</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Recent(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var clamped = take is < 1 or > 200 ? 50 : take;
        var entries = await auditLogger.GetRecentAsync(clamped, cancellationToken);
        return Ok(entries);
    }
}
