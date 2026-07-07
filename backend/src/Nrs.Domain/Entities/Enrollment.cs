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

    /// <summary>
    /// Single character "M" or "F", captured at enrollment so an approved new applicant can be
    /// registered as a person. Nullable: applications predating the field have none.
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>ISO 3166-1 alpha-3 nationality code.</summary>
    public string NationalityCode { get; set; } = null!;

    public EnrollmentType Type { get; set; }

    public EnrollmentStatus Status { get; set; }

    // --- Captured applicant profile ------------------------------------------------------
    // The full biographic + residential record collected at the counter, so an approved
    // application provisions a COMPLETE person (not a shell with empty fields). All nullable
    // at the column level (Oracle can't add mandatory columns to a populated table); the
    // operationally-essential ones are enforced as required at the API boundary instead.

    /// <summary>Place of birth, English.</summary>
    public string? PlaceOfBirthEn { get; set; }

    /// <summary>Place of birth, Arabic.</summary>
    public string? PlaceOfBirthAr { get; set; }

    /// <summary>Mother's full name, English.</summary>
    public string? MotherNameEn { get; set; }

    /// <summary>Mother's full name, Arabic.</summary>
    public string? MotherNameAr { get; set; }

    /// <summary>Marital status (optional — not collected for minors).</summary>
    public MaritalStatus? MaritalStatus { get; set; }

    /// <summary>ABO/Rh blood group, e.g. "O+" (optional).</summary>
    public string? BloodType { get; set; }

    /// <summary>Occupation, English (optional — not collected for minors).</summary>
    public string? OccupationEn { get; set; }

    /// <summary>Occupation, Arabic (optional).</summary>
    public string? OccupationAr { get; set; }

    // Residential address.
    public string? Governorate { get; set; }

    public string? Wilayat { get; set; }

    public string? Village { get; set; }

    public string? Street { get; set; }

    public string? BuildingNumber { get; set; }

    public string? PostalCode { get; set; }

    // Contact details.
    public string? Mobile { get; set; }

    public string? Email { get; set; }

    // Passport (optional — a passport is a separate document, not required for an ID card).
    public string? PassportNumber { get; set; }

    public PassportType? PassportType { get; set; }

    public DateOnly? PassportIssueDate { get; set; }

    public DateOnly? PassportExpiryDate { get; set; }

    /// <summary>Optional free-text note from the operator.</summary>
    public string? Notes { get; set; }

    /// <summary>Operator who created the application (username, or "anonymous").</summary>
    public string CreatedBy { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    // --- Review workflow (Camunda) ------------------------------------------------------
    // All nullable: they arrive as the review progresses, and Oracle cannot add mandatory
    // columns to a populated table anyway.

    /// <summary>Key of the Camunda process instance orchestrating this review, when one exists.</summary>
    public long? ProcessInstanceKey { get; set; }

    /// <summary>
    /// Reviewer who has claimed this application (their username), or null while it sits
    /// unassigned in the queue. Set on claim (PENDING_REVIEW -> UNDER_REVIEW), cleared on
    /// release. Only the assignee may approve or reject.
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>When the current assignee claimed it; null while unassigned.</summary>
    public DateTimeOffset? AssignedAtUtc { get; set; }

    /// <summary>Who decided: a reviewer's username, or "auto-screening" for straight-through approvals.</summary>
    public string? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    /// <summary>Reviewer's reasoning — required for rejections, optional for approvals.</summary>
    public string? DecisionNotes { get; set; }

    /// <summary>Set when the review sat unactioned past the SLA and a supervisor was notified.</summary>
    public DateTimeOffset? EscalatedAtUtc { get; set; }

    /// <summary>
    /// Comma-separated screening flag tokens (e.g. "CRN_NOT_FOUND,DUPLICATE_PENDING") — why
    /// the automated screening routed this application to a human. Null when clean.
    /// </summary>
    public string? ScreeningFlags { get; set; }

    /// <summary>
    /// Screening's risk verdict: "HIGH" routes the review to a supervisor, "NORMAL" (or null,
    /// for applications screened before the field existed) to a regular reviewer.
    /// </summary>
    public string? RiskLevel { get; set; }

    /// <summary>
    /// The reviewer's note explaining what must be fixed, while the application sits in
    /// NEEDS_CORRECTION. Cleared when the operator resubmits.
    /// </summary>
    public string? CorrectionNote { get; set; }
}
