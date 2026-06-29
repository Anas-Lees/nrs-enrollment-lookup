using Nrs.Domain.Enums;

namespace Nrs.Domain.Entities;

/// <summary>
/// An append-only record of a lookup: who did what, against whom, when, and from where.
/// Maps to the AUDIT_ENTRY table. Never updated or deleted in normal operation.
/// </summary>
public class AuditEntry
{
    public long AuditId { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>The operator's username/subject, or "anonymous" on the open POC path.</summary>
    public string Actor { get; set; } = null!;

    public AuditAction Action { get; set; }

    /// <summary>For a profile view, the CRN that was opened.</summary>
    public string? TargetCrn { get; set; }

    /// <summary>For a search, a short summary of the filters used.</summary>
    public string? Criteria { get; set; }

    /// <summary>For a search, the number of matches returned.</summary>
    public int? ResultCount { get; set; }

    /// <summary>Source IP address of the caller, if available.</summary>
    public string? SourceIp { get; set; }
}
