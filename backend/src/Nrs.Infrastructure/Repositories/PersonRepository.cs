using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Application.Search;
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
        // Include the nationality lookup so summary rows can show bilingual names.
        // Typed as IQueryable so the filters below can reassign it after the Include.
        IQueryable<Person> query = db.Persons.AsNoTracking().Include(p => p.Nationality);

        // CRN: prefix match.
        if (!string.IsNullOrWhiteSpace(criteria.Crn))
        {
            var crn = criteria.Crn.Trim();
            query = query.Where(p => p.CivilNumber.StartsWith(crn));
        }

        // Name: partial, fuzzy match across both English and Arabic names. The query is
        // normalized the same way the stored NameSearch column is (diacritics/tatweel
        // stripped, alef/yaa/taa-marbuta/hamza folded, accents removed, lower-cased), so
        // "أحمد", "احمد" and "AHMED" all match. EF translates Contains() to a SQL LIKE that
        // runs in the database against the indexed NameSearch column.
        if (!string.IsNullOrWhiteSpace(criteria.Name))
        {
            var normalized = NameNormalizer.Normalize(criteria.Name);
            if (normalized.Length > 0)
            {
                query = query.Where(p => p.NameSearch != null && p.NameSearch.Contains(normalized));
            }
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

        // Total count of all matches, before paging is applied.
        var totalCount = await query.CountAsync(cancellationToken);

        // Paging is validated at the API boundary and normalised by the service, so the
        // values are trusted here (no second clamp). Stable ordering before Skip/Take keeps
        // pages deterministic.
        var items = await query
            .OrderBy(p => p.FamilyNameEn)
            .ThenBy(p => p.FirstNameEn)
            .ThenBy(p => p.CivilNumber)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
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
            .Include(p => p.Nationality)
            .Include(p => p.Address)
            .Include(p => p.Contact)
            .Include(p => p.IdCards)
            .Include(p => p.Passports)
            .FirstOrDefaultAsync(p => p.CivilNumber == crn, cancellationToken);
    }

    /// <summary>
    /// Loads the person (tracked, with their address and contact), applies the new values —
    /// creating the address/contact rows when the person had none yet — saves, then returns
    /// the refreshed profile. Returns <see langword="null"/> if the CRN is unknown.
    /// </summary>
    /// <param name="crn">The Civil Registration Number of the person to update.</param>
    /// <param name="request">The new address and contact values (already trimmed).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<Person?> UpdateContactDetailsAsync(
        string crn,
        UpdateContactDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tracked load: EF observes the edits (and any inserted dependents) for SaveChanges.
        var person = await db.Persons
            .Include(p => p.Address)
            .Include(p => p.Contact)
            .FirstOrDefaultAsync(p => p.CivilNumber == crn, cancellationToken);

        if (person is null)
        {
            return null;
        }

        // Address and Contact share the CRN as their primary key. A freshly-registered
        // applicant has neither row yet, so create it on first save; otherwise update in place.
        person.Address ??= new Address { CivilNumber = crn };
        person.Address.Governorate = request.Governorate;
        person.Address.Wilayat = request.Wilayat;
        person.Address.Village = request.Village;
        person.Address.Street = request.Street;
        person.Address.BuildingNumber = request.BuildingNumber;
        person.Address.PostalCode = request.PostalCode;

        person.Contact ??= new Contact { CivilNumber = crn };
        person.Contact.Mobile = request.Mobile;
        person.Contact.Email = request.Email;

        await db.SaveChangesAsync(cancellationToken);

        // Re-read (no-tracking, all includes) so the caller gets the full profile to map.
        return await GetByCrnAsync(crn, cancellationToken);
    }
}
