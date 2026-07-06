using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the reviewer who claimed an application sends it back to the operator with a
/// note describing what must be fixed. This is the third review outcome alongside approve and
/// reject — the application settles to NEEDS_CORRECTION and the process waits for a resubmission
/// (or auto-closes when the correction deadline passes). Delegated to the
/// <see cref="IEnrollmentWorkflow"/> so it works with or without a Camunda engine.
/// </summary>
public static class RequestCorrections
{
    /// <summary>Outcome of a request-corrections attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum Outcome
    {
        Applied,
        Accepted,
        NotFound,
        NotUnderReview,

        /// <summary>The caller is not the reviewer who claimed it (403).</summary>
        NotAssignee,

        /// <summary>Another reviewer acted first; this request was NOT recorded (409).</summary>
        Conflict,
    }

    /// <summary>Body of a request-corrections request.</summary>
    public record Request
    {
        /// <summary>What the operator must fix — always required.</summary>
        public string? Note { get; init; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Note).NotEmpty()
                .WithMessage("A note describing the required corrections is required.");
            RuleFor(x => x.Note).MaximumLength(1000);
        }
    }

    public sealed class Handler(NrsDbContext db, IEnrollmentWorkflow workflow)
    {
        public async Task<(Outcome Outcome, EnrollmentDto? Enrollment)> HandleAsync(
            Guid id, Request request, string operatorName, CancellationToken cancellationToken)
        {
            var enrollment = await db.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (enrollment is null)
            {
                return (Outcome.NotFound, null);
            }

            if (enrollment.Status != EnrollmentStatus.UNDER_REVIEW)
            {
                return (Outcome.NotUnderReview, enrollment.ToDto());
            }

            // Ownership: only the reviewer who claimed it may act on it.
            if (!string.Equals(enrollment.AssignedTo, operatorName, StringComparison.Ordinal))
            {
                return (Outcome.NotAssignee, enrollment.ToDto());
            }

            var result = await workflow.DecideAsync(
                id, ReviewOutcome.Corrections, operatorName, EnrollmentRules.TrimToNull(request.Note), cancellationToken);

            if (!result.Recorded)
            {
                return (Outcome.Conflict, result.Enrollment);
            }

            return (result.Settled ? Outcome.Applied : Outcome.Accepted, result.Enrollment);
        }
    }
}
