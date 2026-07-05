using Microsoft.EntityFrameworkCore;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: fetch one enrollment application by id (used to load the edit form).
/// </summary>
public static class GetEnrollment
{
    public sealed class Handler(NrsDbContext db)
    {
        /// <summary>Returns the DTO, or null when no enrollment has the given id.</summary>
        public async Task<EnrollmentDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            return enrollment?.ToDto();
        }
    }
}
