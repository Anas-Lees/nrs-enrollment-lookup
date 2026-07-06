using Nrs.Domain.Entities;
using Nrs.Domain.Enums;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Full view of an enrollment application returned by create / get / update.
/// </summary>
public record EnrollmentDto
{
    /// <summary>Surrogate id and public reference for the application.</summary>
    public Guid Id { get; init; }

    /// <summary>Human-friendly reference number, e.g. "ENR-1A2B3C4D".</summary>
    public string ReferenceNumber { get; init; } = null!;

    /// <summary>CRN of the existing person this continues, or null for a new applicant.</summary>
    public string? CivilNumber { get; init; }

    public string FirstNameEn { get; init; } = null!;

    public string FamilyNameEn { get; init; } = null!;

    public string FirstNameAr { get; init; } = null!;

    public string FamilyNameAr { get; init; } = null!;

    public DateOnly DateOfBirth { get; init; }

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; init; } = null!;

    public EnrollmentType Type { get; init; }

    public EnrollmentStatus Status { get; init; }

    public string? Notes { get; init; }

    public string CreatedBy { get; init; } = null!;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Reviewer who has claimed this application, or null while it sits in the queue.</summary>
    public string? AssignedTo { get; init; }

    /// <summary>When the current assignee claimed it; null while unassigned.</summary>
    public DateTimeOffset? AssignedAtUtc { get; init; }

    /// <summary>Who decided (a reviewer, or "auto-screening"), once the review concluded.</summary>
    public string? DecidedBy { get; init; }

    public DateTimeOffset? DecidedAtUtc { get; init; }

    /// <summary>Reviewer's reasoning — always present for rejections.</summary>
    public string? DecisionNotes { get; init; }

    /// <summary>Set when the review breached its SLA and a supervisor was notified.</summary>
    public DateTimeOffset? EscalatedAtUtc { get; init; }

    /// <summary>Why automated screening routed this to a human (empty when clean).</summary>
    public IReadOnlyList<string> ScreeningFlags { get; init; } = [];
}

/// <summary>
/// One row in the enrollment queue list.
/// </summary>
public record EnrollmentSummaryDto
{
    public Guid Id { get; init; }

    public string ReferenceNumber { get; init; } = null!;

    public string? CivilNumber { get; init; }

    public string FirstNameEn { get; init; } = null!;

    public string FamilyNameEn { get; init; } = null!;

    public string FirstNameAr { get; init; } = null!;

    public string FamilyNameAr { get; init; } = null!;

    public string NationalityCode { get; init; } = null!;

    public EnrollmentType Type { get; init; }

    public EnrollmentStatus Status { get; init; }

    /// <summary>Reviewer who has claimed it, or null while unassigned (drives the queue's "handled by" chip).</summary>
    public string? AssignedTo { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Set when the review breached its SLA (drives the "escalated" chip in the queue).</summary>
    public DateTimeOffset? EscalatedAtUtc { get; init; }
}

/// <summary>Maps the <see cref="Enrollment"/> entity to its outbound DTOs.</summary>
public static class EnrollmentMappingExtensions
{
    public static EnrollmentDto ToDto(this Enrollment e) => new()
    {
        Id = e.Id,
        ReferenceNumber = e.ReferenceNumber,
        CivilNumber = e.CivilNumber,
        FirstNameEn = e.FirstNameEn,
        FamilyNameEn = e.FamilyNameEn,
        FirstNameAr = e.FirstNameAr,
        FamilyNameAr = e.FamilyNameAr,
        DateOfBirth = e.DateOfBirth,
        NationalityCode = e.NationalityCode,
        Type = e.Type,
        Status = e.Status,
        Notes = e.Notes,
        CreatedBy = e.CreatedBy,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
        AssignedTo = e.AssignedTo,
        AssignedAtUtc = e.AssignedAtUtc,
        DecidedBy = e.DecidedBy,
        DecidedAtUtc = e.DecidedAtUtc,
        DecisionNotes = e.DecisionNotes,
        EscalatedAtUtc = e.EscalatedAtUtc,
        ScreeningFlags = string.IsNullOrEmpty(e.ScreeningFlags) ? [] : e.ScreeningFlags.Split(','),
    };

    public static EnrollmentSummaryDto ToSummaryDto(this Enrollment e) => new()
    {
        Id = e.Id,
        ReferenceNumber = e.ReferenceNumber,
        CivilNumber = e.CivilNumber,
        FirstNameEn = e.FirstNameEn,
        FamilyNameEn = e.FamilyNameEn,
        FirstNameAr = e.FirstNameAr,
        FamilyNameAr = e.FamilyNameAr,
        NationalityCode = e.NationalityCode,
        Type = e.Type,
        Status = e.Status,
        AssignedTo = e.AssignedTo,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
        EscalatedAtUtc = e.EscalatedAtUtc,
    };
}
