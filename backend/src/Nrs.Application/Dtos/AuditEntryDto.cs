using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>A single audit record, as returned by the recent-lookups endpoint.</summary>
public record AuditEntryDto
{
    public long AuditId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public string Actor { get; init; } = null!;

    public AuditAction Action { get; init; }

    public string? TargetCrn { get; init; }

    public string? Criteria { get; init; }

    public int? ResultCount { get; init; }

    public string? SourceIp { get; init; }
}
