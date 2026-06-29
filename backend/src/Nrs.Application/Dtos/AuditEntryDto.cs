using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>A single audit record, as returned by the recent-lookups endpoint.</summary>
public record AuditEntryDto
{
    /// <summary>Surrogate identifier of the audit record.</summary>
    public long AuditId { get; init; }

    /// <summary>When the action happened (UTC).</summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>The operator who performed the action (username from the JWT).</summary>
    public string Actor { get; init; } = null!;

    /// <summary>What was done — e.g. a search or a profile view.</summary>
    public AuditAction Action { get; init; }

    /// <summary>The civil registration number that was viewed, when the action targets one person.</summary>
    public string? TargetCrn { get; init; }

    /// <summary>The search terms used, when the action is a search.</summary>
    public string? Criteria { get; init; }

    /// <summary>Number of results returned, when the action is a search.</summary>
    public int? ResultCount { get; init; }

    /// <summary>Client IP address the request came from.</summary>
    public string? SourceIp { get; init; }
}
