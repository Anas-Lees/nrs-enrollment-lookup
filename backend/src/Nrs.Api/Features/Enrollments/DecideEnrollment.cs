using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Messaging;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: an operator approves or rejects an enrollment that is under review. The
/// actual status change is delegated to the <see cref="IEnrollmentWorkflow"/> — Camunda when an
/// engine is configured, otherwise a direct database write — so this slice only owns the
/// preconditions (exists, and is currently UNDER_REVIEW) and the audit event.
/// </summary>
public static class DecideEnrollment
{
    /// <summary>Outcome of a decision attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum Outcome
    {
        /// <summary>The decision was applied and the status has settled (200).</summary>
        Applied,

        /// <summary>The decision was accepted but the workflow has not applied it yet (202).</summary>
        Accepted,

        /// <summary>No enrollment exists with the supplied id (404).</summary>
        NotFound,

        /// <summary>The enrollment is not under review, so it cannot be decided (409).</summary>
        NotUnderReview,
    }

    /// <summary>Body of a decision request.</summary>
    public record Request
    {
        /// <summary>True to approve the application, false to reject it.</summary>
        public bool Approved { get; init; }
    }

    public sealed class Handler(NrsDbContext db, IEnrollmentWorkflow workflow, IEventPublisher publisher)
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

            var result = await workflow.DecideAsync(id, request.Approved, cancellationToken);

            // Attribute the decision (the most security-relevant transition) to the operator,
            // mirroring how create/edit publish their events.
            await publisher.PublishAsync(
                new EnrollmentDecided
                {
                    EnrollmentId = id,
                    ReferenceNumber = enrollment.ReferenceNumber,
                    Operator = operatorName,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    Approved = request.Approved,
                },
                cancellationToken);

            return (result.Settled ? Outcome.Applied : Outcome.Accepted, result.Enrollment);
        }
    }
}
