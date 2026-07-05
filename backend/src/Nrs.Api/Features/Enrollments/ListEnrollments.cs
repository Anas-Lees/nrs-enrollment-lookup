using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: list enrollment applications for the queue view — newest first,
/// optionally filtered by status, with paging clamped to sane bounds.
/// </summary>
public static class ListEnrollments
{
    public sealed class Handler(NrsDbContext db)
    {
        public async Task<PagedResult<EnrollmentSummaryDto>> HandleAsync(
            EnrollmentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<Enrollment> query = db.Enrollments.AsNoTracking();
            if (status is not null)
            {
                query = query.Where(e => e.Status == status.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var entities = await query
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id)
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
