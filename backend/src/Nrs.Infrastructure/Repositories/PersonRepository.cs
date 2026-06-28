using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Domain.Entities;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPersonRepository"/>. Translates
/// <see cref="PersonSearchCriteria"/> into a composable, provider-portable query
/// and returns Domain entities for the Application layer to map into DTOs.
/// </summary>
public class PersonRepository(NrsDbContext db) : IPersonRepository
{
    /// <summary>
    /// Searches persons matching the supplied criteria, applying each filter only
    /// when its value is provided, then returns the requested page together with
    /// the total count of all matches (computed before paging).
    /// </summary>
    /// <param name="criteria">The search filters and paging options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<(IReadOnlyList<Person> Items, int TotalCount)> SearchAsync(
        PersonSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        // Read-only query: no change tracking → faster and lower memory.
        var query = db.Persons.AsNoTracking();

        // CRN: prefix match.
        if (!string.IsNullOrWhiteSpace(criteria.Crn))
        {
            var crn = criteria.Crn.Trim();
            query = query.Where(p => p.CivilNumber.StartsWith(crn));
        }

        // Name: partial, case-insensitive, across both English and Arabic names.
        if (!string.IsNullOrWhiteSpace(criteria.Name))
        {
            var name = criteria.Name.Trim();
            var lowered = name.ToLowerInvariant();

            // EF Core translates string.ToLower() to SQL LOWER() and string.Contains() to
            // LIKE, so the predicate runs IN THE DATABASE (not client-side). The suppressed
            // analyzers all suggest culture/StringComparison overloads that EF cannot
            // translate to SQL — using them would either fail translation (as ToLowerInvariant
            // on a column does) or force in-memory evaluation. Lower-casing both sides keeps
            // English matching case-insensitive; Arabic has no case, so a plain Contains.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(p =>
                p.FirstNameEn.ToLower().Contains(lowered) ||
                p.FamilyNameEn.ToLower().Contains(lowered) ||
                p.FirstNameAr.Contains(name) ||
                p.FamilyNameAr.Contains(name));
#pragma warning restore CA1304, CA1311, CA1862
        }

        // Date of birth: exact match.
        if (criteria.Dob is { } dob)
        {
            query = query.Where(p => p.DateOfBirth == dob);
        }

        // Nationality: exact, upper-cased (ISO 3166-1 alpha-3).
        if (!string.IsNullOrWhiteSpace(criteria.Nationality))
        {
            var nat = criteria.Nationality.Trim().ToUpperInvariant();
            query = query.Where(p => p.NationalityCode == nat);
        }

        // Defensive paging clamps: keep Skip/Take valid regardless of caller input.
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize is < 1 or > 100 ? 20 : criteria.PageSize;

        // Total count of all matches, before paging is applied.
        var totalCount = await query.CountAsync(cancellationToken);

        // Stable ordering is required before Skip/Take so pages are deterministic.
        var items = await query
            .OrderBy(p => p.FamilyNameEn)
            .ThenBy(p => p.FirstNameEn)
            .ThenBy(p => p.CivilNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single person by Civil Registration Number (CRN), eagerly loading
    /// their ID cards and passports, or <see langword="null"/> if no such person exists.
    /// </summary>
    /// <param name="crn">The Civil Registration Number to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<Person?> GetByCrnAsync(string crn, CancellationToken cancellationToken = default)
    {
        return await db.Persons
            .AsNoTracking()
            .Include(p => p.IdCards)
            .Include(p => p.Passports)
            .FirstOrDefaultAsync(p => p.CivilNumber == crn, cancellationToken);
    }
}
