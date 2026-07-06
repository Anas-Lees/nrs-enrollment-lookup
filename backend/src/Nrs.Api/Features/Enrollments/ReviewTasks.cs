using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the reviewer's work queue. Lists the open Camunda review user tasks
/// (joined with their enrollments) oldest-first, and lets a reviewer claim one. With no
/// engine configured the list degrades to the UNDER_REVIEW enrollments straight from the
/// database, so the screen keeps working in plain local dev.
/// </summary>
public static class ReviewTasks
{
    /// <summary>One reviewable item: the Camunda task (when there is one) plus its enrollment.</summary>
    public record ReviewTaskDto
    {
        /// <summary>Camunda user-task key as a string (int64 — unsafe as a JS number), or null with no engine.</summary>
        public string? UserTaskKey { get; init; }

        /// <summary>Reviewer who claimed the task, or null while unclaimed.</summary>
        public string? Assignee { get; init; }

        /// <summary>When the review task was created (falls back to the enrollment's update time).</summary>
        public DateTimeOffset TaskCreatedAtUtc { get; init; }

        public EnrollmentDto Enrollment { get; init; } = null!;
    }

    public sealed class ListHandler(NrsDbContext db, IEnumerable<ICamundaClient> camunda)
    {
        // IEnumerable: zero clients registered when Camunda is off, one when on.
        private readonly ICamundaClient? _camunda = camunda.FirstOrDefault();

        public async Task<IReadOnlyList<ReviewTaskDto>> HandleAsync(CancellationToken cancellationToken)
        {
            if (_camunda is not null)
            {
                try
                {
                    return await ListFromCamundaAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    // Engine down: degrade to the database view rather than 500 the screen.
                }
            }

            // No engine (or engine unreachable): the review queue is everything under review.
            var underReview = await db.Enrollments.AsNoTracking()
                .Where(e => e.Status == EnrollmentStatus.UNDER_REVIEW)
                .OrderBy(e => e.UpdatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);
            return underReview
                .Select(e => new ReviewTaskDto
                {
                    UserTaskKey = null,
                    Assignee = null,
                    TaskCreatedAtUtc = e.UpdatedAtUtc,
                    Enrollment = e.ToDto(),
                })
                .ToList();
        }

        private async Task<IReadOnlyList<ReviewTaskDto>> ListFromCamundaAsync(CancellationToken cancellationToken)
        {
            var tasks = await _camunda!.SearchUserTasksAsync(
                "CREATED", CamundaEnrollmentWorkflow.ProcessId, processInstanceKey: null, cancellationToken);
            var reviewTasks = tasks
                .Where(t => t.ElementId == CamundaEnrollmentWorkflow.ReviewTaskElementId)
                .ToList();
            if (reviewTasks.Count == 0)
            {
                return [];
            }

            var instanceKeys = reviewTasks.Select(t => t.ProcessInstanceKey).ToList();
            // Only enrollments still awaiting a decision. A decision applied out-of-band — via
            // the engine-down / orphan direct-write fallback — updates Oracle but cannot complete
            // the Camunda user task, leaving a "ghost" task that lingers as CREATED. The
            // enrollment status is the source of truth for what still needs a human; filtering on
            // UNDER_REVIEW keeps those already-decided ghosts out of the queue (deciding one would
            // only 409 forever).
            var enrollments = await db.Enrollments.AsNoTracking()
                .Where(e => e.ProcessInstanceKey != null
                            && instanceKeys.Contains(e.ProcessInstanceKey.Value)
                            && e.Status == EnrollmentStatus.UNDER_REVIEW)
                .ToDictionaryAsync(e => e.ProcessInstanceKey!.Value, cancellationToken);

            return reviewTasks
                .Where(t => enrollments.ContainsKey(t.ProcessInstanceKey))
                .OrderBy(t => t.CreationDate) // FIFO: oldest applications first
                .Select(t => new ReviewTaskDto
                {
                    UserTaskKey = t.UserTaskKey.ToString(CultureInfo.InvariantCulture),
                    Assignee = t.Assignee,
                    TaskCreatedAtUtc = t.CreationDate,
                    Enrollment = enrollments[t.ProcessInstanceKey].ToDto(),
                })
                .ToList();
        }
    }

    /// <summary>What happened to a claim attempt, mapped to an HTTP status by the endpoint.</summary>
    public enum ClaimOutcome
    {
        Claimed,

        /// <summary>Already claimed by someone else, or the task no longer exists (409).</summary>
        Taken,

        /// <summary>No workflow engine is configured (404).</summary>
        NoEngine,
    }

    public sealed class ClaimHandler(IEnumerable<ICamundaClient> camunda)
    {
        private readonly ICamundaClient? _camunda = camunda.FirstOrDefault();

        public async Task<ClaimOutcome> HandleAsync(long userTaskKey, string assignee, CancellationToken cancellationToken)
        {
            if (_camunda is null)
            {
                return ClaimOutcome.NoEngine;
            }

            var claimed = await _camunda.AssignUserTaskAsync(userTaskKey, assignee, cancellationToken);
            return claimed ? ClaimOutcome.Claimed : ClaimOutcome.Taken;
        }
    }
}
