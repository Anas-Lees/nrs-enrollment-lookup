using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// The automated screening step of the review process: cheap registry checks that decide
/// whether an application needs a human at all. A <b>clean renewal</b> (known CRN, active
/// record, matching name, no duplicates) is auto-approvable — straight-through processing;
/// anything flagged goes to a reviewer, with the flags carried on the process so the
/// reviewer sees exactly why the application was routed to them.
/// </summary>
public static class EnrollmentScreening
{
    // Flag tokens — stable identifiers, translated by the frontends.
    public const string CrnNotFound = "CRN_NOT_FOUND";
    public const string RegistryRecordNotActive = "REGISTRY_RECORD_NOT_ACTIVE";
    public const string NameMismatch = "NAME_MISMATCH";
    public const string DuplicatePending = "DUPLICATE_PENDING";
    public const string MinorApplicant = "MINOR_APPLICANT";

    public sealed record Result(bool AutoApprove, IReadOnlyList<string> Flags);

    public static async Task<Result> ScreenAsync(NrsDbContext db, Enrollment enrollment, CancellationToken cancellationToken)
    {
        var flags = new List<string>();

        // 1) Continuing applications must reference a real, active registry record whose
        //    family name matches what the operator captured.
        if (!string.IsNullOrWhiteSpace(enrollment.CivilNumber))
        {
            var person = await db.Persons.AsNoTracking()
                .FirstOrDefaultAsync(p => p.CivilNumber == enrollment.CivilNumber, cancellationToken);
            if (person is null)
            {
                flags.Add(CrnNotFound);
            }
            else
            {
                if (person.Status != PersonStatus.ACTIVE)
                {
                    flags.Add(RegistryRecordNotActive);
                }

                if (!string.Equals(person.FamilyNameEn.Trim(), enrollment.FamilyNameEn.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    flags.Add(NameMismatch);
                }
            }
        }

        // 2) Another live application for the same person is a red flag (double submission,
        //    or two counters serving the same applicant).
        var hasDuplicate = await db.Enrollments.AsNoTracking().AnyAsync(
            e => e.Id != enrollment.Id
                 && (e.Status == EnrollmentStatus.SUBMITTED || e.Status == EnrollmentStatus.UNDER_REVIEW)
                 && (
                     (enrollment.CivilNumber != null && e.CivilNumber == enrollment.CivilNumber)
                     || (e.FirstNameEn == enrollment.FirstNameEn
                         && e.FamilyNameEn == enrollment.FamilyNameEn
                         && e.DateOfBirth == enrollment.DateOfBirth)
                 ),
            cancellationToken);
        if (hasDuplicate)
        {
            flags.Add(DuplicatePending);
        }

        // 3) First cards for minors need guardian documentation — always a human check.
        if (enrollment.Type == EnrollmentType.NEW_CARD && AgeInYears(enrollment.DateOfBirth) < 18)
        {
            flags.Add(MinorApplicant);
        }

        // Straight-through processing: only a clean renewal of a known record skips review.
        var autoApprove = flags.Count == 0
                          && enrollment.Type == EnrollmentType.RENEWAL
                          && !string.IsNullOrWhiteSpace(enrollment.CivilNumber);

        return new Result(autoApprove, flags);
    }

    private static int AgeInYears(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
