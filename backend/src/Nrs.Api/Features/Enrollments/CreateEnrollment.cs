using FluentValidation;
using Nrs.Api.Features.Enrollments.Messaging;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: create a new enrollment application. Everything this operation needs —
/// the request shape, its validation rules, and the handler — lives in this one file.
/// A created application starts as SUBMITTED and an <see cref="EnrollmentSubmitted"/> event
/// is published so the background consumer can pick it up for review.
/// </summary>
public static class CreateEnrollment
{
    /// <summary>Body of a create-enrollment request.</summary>
    public record Request
    {
        /// <summary>CRN of an existing person to continue from, or null for a new applicant.</summary>
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

        // --- Captured applicant profile (so an approved application provisions a complete person) ---

        /// <summary>Place of birth, English (required).</summary>
        public string? PlaceOfBirthEn { get; init; }

        /// <summary>Place of birth, Arabic (required).</summary>
        public string? PlaceOfBirthAr { get; init; }

        /// <summary>Mother's name, English (required).</summary>
        public string? MotherNameEn { get; init; }

        /// <summary>Mother's name, Arabic (required).</summary>
        public string? MotherNameAr { get; init; }

        /// <summary>Marital status (optional — not collected for minors).</summary>
        public MaritalStatus? MaritalStatus { get; init; }

        /// <summary>ABO/Rh blood group, e.g. "O+" (optional).</summary>
        public string? BloodType { get; init; }

        public string? OccupationEn { get; init; }

        public string? OccupationAr { get; init; }

        /// <summary>Governorate — one of Oman's 11 (required).</summary>
        public string? Governorate { get; init; }

        /// <summary>Wilayat within the governorate (required).</summary>
        public string? Wilayat { get; init; }

        public string? Village { get; init; }

        public string? Street { get; init; }

        public string? BuildingNumber { get; init; }

        public string? PostalCode { get; init; }

        /// <summary>Mobile number, e.g. "+96891234567" (required).</summary>
        public string? Mobile { get; init; }

        public string? Email { get; init; }

        // Passport — optional (a passport is a separate document, not needed for an ID card).
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

            // Full-record capture: the essentials are required so no approved application
            // provisions a person with empty biographic / address / contact fields.
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

            // Optional fields — validated only when supplied.
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

            // Passport is optional as a whole, but a number needs a type to be meaningful.
            RuleFor(x => x.PassportNumber).MaximumLength(20);
            RuleFor(x => x.PassportType).NotNull()
                .When(x => !string.IsNullOrWhiteSpace(x.PassportNumber))
                .WithMessage("Select the passport type.");
            RuleFor(x => x.PassportType).IsInEnum().When(x => x.PassportType.HasValue);
        }
    }

    public sealed class Handler(NrsDbContext db, IEventPublisher publisher, IEnrollmentWorkflow workflow)
    {
        public async Task<EnrollmentDto> HandleAsync(Request request, string operatorName, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();

            var enrollment = new Enrollment
            {
                Id = id,
                ReferenceNumber = EnrollmentRules.NewReferenceNumber(id),
                CivilNumber = EnrollmentRules.TrimToNull(request.CivilNumber),
                FirstNameEn = request.FirstNameEn.Trim(),
                FamilyNameEn = request.FamilyNameEn.Trim(),
                FirstNameAr = request.FirstNameAr.Trim(),
                FamilyNameAr = request.FamilyNameAr.Trim(),
                DateOfBirth = request.DateOfBirth,
                Gender = EnrollmentRules.TrimToNull(request.Gender)?.ToUpperInvariant(),
                NationalityCode = request.NationalityCode.Trim().ToUpperInvariant(),
                Type = request.Type,
                Status = EnrollmentStatus.SUBMITTED,
                PlaceOfBirthEn = EnrollmentRules.TrimToNull(request.PlaceOfBirthEn),
                PlaceOfBirthAr = EnrollmentRules.TrimToNull(request.PlaceOfBirthAr),
                MotherNameEn = EnrollmentRules.TrimToNull(request.MotherNameEn),
                MotherNameAr = EnrollmentRules.TrimToNull(request.MotherNameAr),
                MaritalStatus = request.MaritalStatus,
                BloodType = EnrollmentRules.TrimToNull(request.BloodType),
                OccupationEn = EnrollmentRules.TrimToNull(request.OccupationEn),
                OccupationAr = EnrollmentRules.TrimToNull(request.OccupationAr),
                Governorate = EnrollmentRules.TrimToNull(request.Governorate),
                Wilayat = EnrollmentRules.TrimToNull(request.Wilayat),
                Village = EnrollmentRules.TrimToNull(request.Village),
                Street = EnrollmentRules.TrimToNull(request.Street),
                BuildingNumber = EnrollmentRules.TrimToNull(request.BuildingNumber),
                PostalCode = EnrollmentRules.TrimToNull(request.PostalCode),
                Mobile = EnrollmentRules.TrimToNull(request.Mobile),
                Email = EnrollmentRules.TrimToNull(request.Email),
                PassportNumber = EnrollmentRules.TrimToNull(request.PassportNumber),
                PassportType = request.PassportType,
                PassportIssueDate = request.PassportIssueDate,
                PassportExpiryDate = request.PassportExpiryDate,
                Notes = EnrollmentRules.TrimToNull(request.Notes),
                CreatedBy = operatorName,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync(cancellationToken);

            await publisher.PublishAsync(
                new EnrollmentSubmitted
                {
                    EnrollmentId = id,
                    ReferenceNumber = enrollment.ReferenceNumber,
                    Operator = operatorName,
                    OccurredAtUtc = now,
                },
                cancellationToken);

            // Kick off the review workflow (Camunda, or a no-op when no engine is configured).
            await workflow.OnSubmittedAsync(enrollment, cancellationToken);

            return enrollment.ToDto();
        }
    }
}
