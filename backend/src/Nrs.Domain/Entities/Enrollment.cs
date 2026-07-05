using Nrs.Domain.Enums;

namespace Nrs.Domain.Entities;

/// <summary>
/// An enrollment application captured at a counter. It may continue from an existing
/// person (<see cref="CivilNumber"/> set) or start fresh for a new applicant. Maps to
/// the ENROLLMENT table.
/// </summary>
public class Enrollment
{
    /// <summary>Surrogate primary key and public reference for the application.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-friendly reference number shown to operators, e.g. "ENR-1A2B3C4D".</summary>
    public string ReferenceNumber { get; set; } = null!;

    /// <summary>
    /// CRN of the existing person this enrollment continues, or null for a brand-new applicant.
    /// </summary>
    public string? CivilNumber { get; set; }

    public string FirstNameEn { get; set; } = null!;

    public string FamilyNameEn { get; set; } = null!;

    public string FirstNameAr { get; set; } = null!;

    public string FamilyNameAr { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; set; } = null!;

    public EnrollmentType Type { get; set; }

    public EnrollmentStatus Status { get; set; }

    /// <summary>Optional free-text note from the operator.</summary>
    public string? Notes { get; set; }

    /// <summary>Operator who created the application (username, or "anonymous").</summary>
    public string CreatedBy { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
