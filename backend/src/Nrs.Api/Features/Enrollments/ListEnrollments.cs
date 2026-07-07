using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: list enrollment applications for the queue view — newest first by default,
/// optionally filtered by status and ordered by a <paramref name="sort"/> key (server-side so
/// it spans every page, not just the one on screen), with paging clamped to sane bounds.
/// </summary>
public static class ListEnrollments
{
    public sealed class Handler(NrsDbContext db)
    {
        public async Task<PagedResult<EnrollmentSummaryDto>> HandleAsync(
            EnrollmentStatus? status, int page, int pageSize, string? sort, CancellationToken cancellationToken)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<Enrollment> query = db.Enrollments.AsNoTracking();
            if (status is not null)
            {
                query = query.Where(e => e.Status == status.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            // Server-side ordering so the sort covers all pages. Enum columns are stored as their
            // string names, so ordering by Type/Status sorts alphabetically by token.
            query = sort switch
            {
                "created-asc" => query.OrderBy(e => e.CreatedAtUtc).ThenBy(e => e.Id),
                "name-asc" => query.OrderBy(e => e.FamilyNameEn).ThenBy(e => e.FirstNameEn),
                "name-desc" => query.OrderByDescending(e => e.FamilyNameEn).ThenByDescending(e => e.FirstNameEn),
                "type-asc" => query.OrderBy(e => e.Type).ThenByDescending(e => e.CreatedAtUtc),
                "status-asc" => query.OrderBy(e => e.Status).ThenByDescending(e => e.CreatedAtUtc),
                _ => query.OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id),
            };

            var entities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<EnrollmentSummaryDto>
            {
                Items = entities.Select(e => e.ToSummaryDto()).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            };
        }
    }
}
