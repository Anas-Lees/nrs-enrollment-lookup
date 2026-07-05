using FluentValidation;
using Nrs.Api.Features.Enrollments.Messaging;
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

        public string NationalityCode { get; init; } = null!;

        public EnrollmentType Type { get; init; }

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
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.Notes).MaximumLength(1000);
        }
    }

    public sealed class Handler(NrsDbContext db, IEventPublisher publisher)
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
                NationalityCode = request.NationalityCode.Trim().ToUpperInvariant(),
                Type = request.Type,
                Status = EnrollmentStatus.SUBMITTED,
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

            return enrollment.ToDto();
        }
    }
}
