using Nrs.Application.Dtos;
using Nrs.Domain.Enums;

namespace Nrs.Application.Interfaces;

/// <summary>
/// Records and reads the append-only audit trail of applicant lookups. Implemented in
/// Infrastructure; written from the API audit filter so every search and profile view
/// is accountable.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Append one audit record. Stamps the timestamp at write time.</summary>
    Task LogAsync(
        string actor,
        string? sourceIp,
        AuditAction action,
        string? targetCrn,
        string? criteria,
        int? resultCount,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent audit records, newest first (for the recent-lookups view).</summary>
    Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
