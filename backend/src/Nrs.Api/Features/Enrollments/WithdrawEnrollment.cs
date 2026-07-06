using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the operator withdraws an application before it is decided — the applicant
/// pulled out, or it was captured in error. This cancels the review wherever it is (waiting to
/// be screened, in the queue, claimed, or awaiting corrections) and settles it to WITHDRAWN.
/// With Camunda it correlates an interrupting message that cancels the whole process instance;
/// without an engine it writes WITHDRAWN directly. Delegated to the <see cref="IEnrollmentWorkflow"/>.
/// </summary>
public static class WithdrawEnrollment
{
    /// <summary>Outcome of a withdraw attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum Outcome
    {
        Withdrawn,
        Accepted,
        NotFound,

        /// <summary>The application is already concluded, so it cannot be withdrawn (409).</summary>
        AlreadyConcluded,
    }

    /// <summary>Body of a withdraw request.</summary>
    public record Request
    {
        /// <summary>Optional reason for the withdrawal, recorded on the application.</summary>
        public string? Reason { get; init; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator() => RuleFor(x => x.Reason).MaximumLength(1000);
    }

    private static readonly EnrollmentStatus[] Concluded =
        [EnrollmentStatus.APPROVED, EnrollmentStatus.REJECTED, EnrollmentStatus.WITHDRAWN];

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

            if (Concluded.Contains(enrollment.Status))
            {
                return (Outcome.AlreadyConcluded, enrollment.ToDto());
            }

            var result = await workflow.WithdrawAsync(
                id, operatorName, EnrollmentRules.TrimToNull(request.Reason), cancellationToken);

            if (!result.Recorded)
            {
                return (Outcome.AlreadyConcluded, result.Enrollment);
            }

            return (result.Settled ? Outcome.Withdrawn : Outcome.Accepted, result.Enrollment);
        }
    }
}
