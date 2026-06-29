using Microsoft.EntityFrameworkCore;
using Nrs.Application.Dtos;
using Nrs.Application.Interfaces;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Infrastructure.Repositories;

/// <summary>EF Core implementation of the audit trail (writes to AUDIT_ENTRY).</summary>
public class AuditLogger(NrsDbContext db) : IAuditLogger
{
    public async Task LogAsync(
        string actor,
        string? sourceIp,
        AuditAction action,
        string? targetCrn,
        string? criteria,
        int? resultCount,
        CancellationToken cancellationToken = default)
    {
        // Defensive: clamp to the AUDIT_ENTRY column widths so a hostile/over-long input can
        // never make the audit INSERT overflow a column (ORA-12899) and turn into a 500.
        db.AuditEntries.Add(new AuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Actor = Clamp(actor, 100)!,
            Action = action,
            TargetCrn = Clamp(targetCrn, 9),
            Criteria = Clamp(criteria, 400),
            ResultCount = resultCount,
            SourceIp = Clamp(sourceIp, 45),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clamp(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    public async Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        return await db.AuditEntries
            .AsNoTracking()
            // Order by the monotonic identity key (insert order == chronological). Avoids
            // ordering by DateTimeOffset, which SQLite cannot translate to a SQL ORDER BY.
            .OrderByDescending(a => a.AuditId)
            .Take(take)
            .Select(a => new AuditEntryDto
            {
                AuditId = a.AuditId,
                TimestampUtc = a.TimestampUtc,
                Actor = a.Actor,
                Action = a.Action,
                TargetCrn = a.TargetCrn,
                Criteria = a.Criteria,
                ResultCount = a.ResultCount,
                SourceIp = a.SourceIp,
            })
            .ToListAsync(cancellationToken);
    }
}
