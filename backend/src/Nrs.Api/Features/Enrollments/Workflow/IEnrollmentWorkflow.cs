using Nrs.Domain.Entities;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// The review lifecycle of an enrollment, behind one seam so the rest of the feature does not
/// care whether Camunda is orchestrating it. Two implementations exist:
/// <list type="bullet">
///   <item><see cref="CamundaEnrollmentWorkflow"/> — starts a BPMN instance and correlates the
///   decision message; a background worker applies the status changes.</item>
///   <item><see cref="DbEnrollmentWorkflow"/> — no engine configured, so a decision is written
///   straight to the database. Keeps approve/reject working in plain local dev and tests.</item>
/// </list>
/// </summary>
public interface IEnrollmentWorkflow
{
    /// <summary>Kicks off the review for a freshly submitted enrollment (best-effort).</summary>
    Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken);

    /// <summary>
    /// Records an operator's approve/reject decision and returns the enrollment as it stands
    /// afterwards. The caller has already checked the enrollment exists and is under review.
    /// </summary>
    Task<DecisionResult> DecideAsync(Guid enrollmentId, bool approved, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a decision: the enrollment as it stands, and whether its status has actually
/// <paramref name="Settled"/> to APPROVED/REJECTED. With Camunda the status is applied
/// asynchronously by the job worker, so a decision can be accepted but not yet settled — the
/// direct-to-database fallback always settles synchronously.
/// </summary>
public sealed record DecisionResult(EnrollmentDto Enrollment, bool Settled);
