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

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
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
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
    };
}
