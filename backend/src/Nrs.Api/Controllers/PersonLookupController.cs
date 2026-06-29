using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nrs.Api.Filters;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;

namespace Nrs.Api.Controllers;

/// <summary>
/// Applicant lookup endpoints. Thin by design: validate/bind the request, call the
/// service, and shape the HTTP response. No business logic lives here.
/// Every action is recorded to the audit trail via <see cref="AuditActionFilter"/>,
/// and the endpoints are rate-limited per operator/IP to deter enumeration.
/// </summary>
[ApiController]
[Route("api/v1/persons")]
[Produces("application/json")]
[ServiceFilter(typeof(AuditActionFilter))]
[EnableRateLimiting("lookup")]
public class PersonLookupController(IPersonLookupService service) : ControllerBase
{
    /// <summary>
    /// Searches persons by CRN, name (partial, Arabic or English), date of birth and
    /// nationality — individually or combined — returning a paginated list of matches.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<PersonSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PersonSummaryDto>>> Search(
        [FromQuery] PersonSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(criteria, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a person's full profile — biographic data plus their ID cards and
    /// passports — or 404 if no person has the supplied CRN.
    /// </summary>
    // CRN is a civil-registration number: digits only, at most 9 (the AUDIT_ENTRY.TARGET_CRN
    // column width). The route constraint rejects malformed/over-long values with a 404 before
    // they reach the audit insert (an over-long value previously surfaced as a 500).
    [HttpGet("{crn:regex(^\\d{{1,9}}$)}")]
    [ProducesResponseType(typeof(PersonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonDto>> GetByCrn(string crn, CancellationToken cancellationToken)
    {
        var person = await service.GetByCrnAsync(crn, cancellationToken);
        return person is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Person not found",
                detail: $"No person exists with CRN '{crn}'.")
            : Ok(person);
    }
}
