using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Messaging;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: edit an existing enrollment application. Biographic fields, type and
/// notes are editable; <see cref="EnrollmentStatus"/> is NOT — the status is driven by the
/// review workflow (the background consumer), not by manual edits. An
/// <see cref="EnrollmentUpdated"/> event is published after a successful save.
/// </summary>
public static class UpdateEnrollment
{
    public record Request
    {
        public string? CivilNumber { get; init; }

        public string FirstNameEn { get; init; } = null!;

        public string FamilyNameEn { get; init; } = null!;

        public string FirstNameAr { get; init; } = null!;

        public string FamilyNameAr { get; init; } = null!;

        public DateOnly DateOfBirth { get; init; }

        /// <summary>"M" or "F" — needed to register a new applicant as a person once approved.</summary>
        public string? Gender { get; init; }

        public string NationalityCode { get; init; } = null!;

        public EnrollmentType Type { get; init; }

        // --- Captured applicant profile (mirrors create; editable until the review concludes) ---
        public string? PlaceOfBirthEn { get; init; }
        public string? PlaceOfBirthAr { get; init; }
        public string? MotherNameEn { get; init; }
        public string? MotherNameAr { get; init; }
        public MaritalStatus? MaritalStatus { get; init; }
        public string? BloodType { get; init; }
        public string? OccupationEn { get; init; }
        public string? OccupationAr { get; init; }
        public string? Governorate { get; init; }
        public string? Wilayat { get; init; }
        public string? Village { get; init; }
        public string? Street { get; init; }
        public string? BuildingNumber { get; init; }
        public string? PostalCode { get; init; }
        public string? Mobile { get; init; }
        public string? Email { get; init; }
        public string? PassportNumber { get; init; }
        public PassportType? PassportType { get; init; }
        public DateOnly? PassportIssueDate { get; init; }
        public DateOnly? PassportExpiryDate { get; init; }

        public string? Notes { get; init; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FirstNameEn).NotEmpty().MaximumLength(100);
            RuleFor(x => x.FamilyNameEn).NotEmpty().MaximumLength(100);
            RuleFor(x => x.FirstNameAr).NotEmpty().MaximumLength(100);
            RuleFor(x => x.FamilyNameAr).NotEmpty().MaximumLength(100);
            RuleFor(x => x.NationalityCode).NotEmpty().Matches("^[A-Za-z]{3}$")
                .WithMessage("Nationality must be a 3-letter ISO code.");
            RuleFor(x => x.CivilNumber).Matches(@"^\d{1,9}$")
                .When(x => !string.IsNullOrWhiteSpace(x.CivilNumber))
                .WithMessage("CRN must be 1 to 9 digits.");
            RuleFor(x => x.DateOfBirth).Must(EnrollmentRules.BeAPlausibleDateOfBirth)
                .WithMessage("Date of birth must be a real past date.");
            RuleFor(x => x.Gender).Must(g => g is "M" or "F")
                .When(x => !string.IsNullOrWhiteSpace(x.Gender))
                .WithMessage("Gender must be 'M' or 'F'.");
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.Notes).MaximumLength(1000);

            // Same full-record rules as create — an edit must keep the essentials complete.
            RuleFor(x => x.PlaceOfBirthEn).NotEmpty().MaximumLength(80);
            RuleFor(x => x.PlaceOfBirthAr).NotEmpty().MaximumLength(80);
            RuleFor(x => x.MotherNameEn).NotEmpty().MaximumLength(150);
            RuleFor(x => x.MotherNameAr).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Governorate).NotEmpty()
                .Must(g => g is not null && Persons.UpdateContactDetailsRequestValidator.Governorates.Contains(g))
                .WithMessage("Governorate must be one of Oman's 11 governorates.");
            RuleFor(x => x.Wilayat).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Mobile).NotEmpty()
                .Matches(@"^\+?[0-9][0-9\s]{6,18}$")
                .WithMessage("Enter a valid mobile number, e.g. +96891234567.");
            RuleFor(x => x.MaritalStatus).IsInEnum().When(x => x.MaritalStatus.HasValue);
            RuleFor(x => x.BloodType).MaximumLength(3);
            RuleFor(x => x.OccupationEn).MaximumLength(100);
            RuleFor(x => x.OccupationAr).MaximumLength(100);
            RuleFor(x => x.Village).MaximumLength(80);
            RuleFor(x => x.Street).MaximumLength(120);
            RuleFor(x => x.BuildingNumber).MaximumLength(20);
            RuleFor(x => x.PostalCode).Matches("^[0-9]{3,10}$")
                .When(x => !string.IsNullOrWhiteSpace(x.PostalCode))
                .WithMessage("Postal code must be 3–10 digits.");
            RuleFor(x => x.Email).EmailAddress().MaximumLength(120)
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
            RuleFor(x => x.PassportNumber).MaximumLength(20);
            RuleFor(x => x.PassportType).NotNull()
                .When(x => !string.IsNullOrWhiteSpace(x.PassportNumber))
                .WithMessage("Select the passport type.");
            RuleFor(x => x.PassportType).IsInEnum().When(x => x.PassportType.HasValue);
        }
    }

    public sealed class Handler(NrsDbContext db, IEventPublisher publisher)
    {
        /// <summary>Returns the updated DTO, or null when no enrollment has the given id.</summary>
        public async Task<EnrollmentDto?> HandleAsync(
            Guid id, Request request, string operatorName, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (enrollment is null)
            {
                return null;
            }

            enrollment.CivilNumber = EnrollmentRules.TrimToNull(request.CivilNumber);
            enrollment.FirstNameEn = request.FirstNameEn.Trim();
            enrollment.FamilyNameEn = request.FamilyNameEn.Trim();
            enrollment.FirstNameAr = request.FirstNameAr.Trim();
            enrollment.FamilyNameAr = request.FamilyNameAr.Trim();
            enrollment.DateOfBirth = request.DateOfBirth;
            enrollment.Gender = EnrollmentRules.TrimToNull(request.Gender)?.ToUpperInvariant();
            enrollment.NationalityCode = request.NationalityCode.Trim().ToUpperInvariant();
            enrollment.Type = request.Type;
            enrollment.PlaceOfBirthEn = EnrollmentRules.TrimToNull(request.PlaceOfBirthEn);
            enrollment.PlaceOfBirthAr = EnrollmentRules.TrimToNull(request.PlaceOfBirthAr);
            enrollment.MotherNameEn = EnrollmentRules.TrimToNull(request.MotherNameEn);
            enrollment.MotherNameAr = EnrollmentRules.TrimToNull(request.MotherNameAr);
            enrollment.MaritalStatus = request.MaritalStatus;
            enrollment.BloodType = EnrollmentRules.TrimToNull(request.BloodType);
            enrollment.OccupationEn = EnrollmentRules.TrimToNull(request.OccupationEn);
            enrollment.OccupationAr = EnrollmentRules.TrimToNull(request.OccupationAr);
            enrollment.Governorate = EnrollmentRules.TrimToNull(request.Governorate);
            enrollment.Wilayat = EnrollmentRules.TrimToNull(request.Wilayat);
            enrollment.Village = EnrollmentRules.TrimToNull(request.Village);
            enrollment.Street = EnrollmentRules.TrimToNull(request.Street);
            enrollment.BuildingNumber = EnrollmentRules.TrimToNull(request.BuildingNumber);
            enrollment.PostalCode = EnrollmentRules.TrimToNull(request.PostalCode);
            enrollment.Mobile = EnrollmentRules.TrimToNull(request.Mobile);
            enrollment.Email = EnrollmentRules.TrimToNull(request.Email);
            enrollment.PassportNumber = EnrollmentRules.TrimToNull(request.PassportNumber);
            enrollment.PassportType = request.PassportType;
            enrollment.PassportIssueDate = request.PassportIssueDate;
            enrollment.PassportExpiryDate = request.PassportExpiryDate;
            enrollment.Notes = EnrollmentRules.TrimToNull(request.Notes);
            enrollment.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            await publisher.PublishAsync(
                new EnrollmentUpdated
                {
                    EnrollmentId = enrollment.Id,
                    ReferenceNumber = enrollment.ReferenceNumber,
                    Operator = operatorName,
                    OccurredAtUtc = enrollment.UpdatedAtUtc,
                },
                cancellationToken);

            return enrollment.ToDto();
        }
    }
}
