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
            enrollment.NationalityCode = request.NationalityCode.Trim().ToUpperInvariant();
            enrollment.Type = request.Type;
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
