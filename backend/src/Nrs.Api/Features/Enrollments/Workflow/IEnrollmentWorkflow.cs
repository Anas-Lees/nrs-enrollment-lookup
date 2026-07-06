using Nrs.Domain.Entities;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>What a reviewer decided about an application.</summary>
public enum ReviewOutcome
{
    /// <summary>Approve — the application proceeds to fulfilment.</summary>
    Approved,

    /// <summary>Reject — the application is refused (a reason is required).</summary>
    Rejected,

    /// <summary>Send back to the operator with a note; the process waits for a resubmission.</summary>
    Corrections,
}

/// <summary>
/// The review lifecycle of an enrollment, behind one seam so the rest of the feature does not
/// care whether Camunda is orchestrating it. Two implementations exist:
/// <list type="bullet">
///   <item><see cref="CamundaEnrollmentWorkflow"/> — starts a BPMN instance, completes the
///   reviewer user task, and correlates the withdraw / resubmit messages; a background worker
///   applies the status changes.</item>
///   <item><see cref="DbEnrollmentWorkflow"/> — no engine configured, so every transition is
///   written straight to the database. Keeps the feature working in plain local dev and tests.</item>
/// </list>
/// </summary>
public interface IEnrollmentWorkflow
{
    /// <summary>Kicks off the review for a freshly submitted enrollment (best-effort).</summary>
    Task OnSubmittedAsync(Enrollment enrollment, CancellationToken cancellationToken);

    /// <summary>
    /// Records a reviewer's decision — approve, reject, or request corrections (with who
    /// decided and their note) — and returns the enrollment as it stands afterwards. The caller
    /// has already checked the enrollment exists, is under review, and is theirs to decide.
    /// </summary>
    Task<DecisionResult> DecideAsync(
        Guid enrollmentId, ReviewOutcome outcome, string decidedBy, string? notes, CancellationToken cancellationToken);

    /// <summary>
    /// Resubmits an application the operator has corrected: it re-enters screening and, if still
    /// flagged, the review queue. The caller has already reset the row to SUBMITTED.
    /// </summary>
    Task ResubmitAsync(Enrollment enrollment, CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws an application before a decision (the applicant pulled out): the review is
    /// cancelled and the row settles to WITHDRAWN. Returns the enrollment as it stands after.
    /// </summary>
    Task<DecisionResult> WithdrawAsync(
        Guid enrollmentId, string withdrawnBy, string? reason, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a transition: the enrollment as it stands, whether its status has actually
/// <paramref name="Settled"/> to a terminal-for-this-step value (APPROVED / REJECTED /
/// NEEDS_CORRECTION / WITHDRAWN), and whether this caller's action was actually
/// <paramref name="Recorded"/>. With Camunda the status is applied asynchronously by the job
/// worker, so an action can be accepted but not yet settled; and when two reviewers race for
/// the same task, the loser's action is NOT recorded — the caller must report that honestly
/// (409) rather than pretend the loser's choice won.
/// </summary>
public sealed record DecisionResult(EnrollmentDto Enrollment, bool Settled, bool Recorded = true);
