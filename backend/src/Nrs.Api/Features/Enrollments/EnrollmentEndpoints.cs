using Nrs.Api.Features.Enrollments.Validation;
using Nrs.Application.Dtos;
using Nrs.Domain.Enums;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Maps the enrollment vertical slices onto minimal-API routes under
/// <c>/api/v1/enrollments</c>. Each route delegates straight to its slice handler — the
/// controller/service/repository layering used by the lookup feature is deliberately
/// absent here, which is the point of the vertical-slice style.
/// </summary>
public static class EnrollmentEndpoints
{
    /// <summary>
    /// <paramref name="enforceRoles"/> is true when auth is on: deciding and reviewing then
    /// require the <c>reviewer</c> role ("CanReview" policy), not just any operator. With auth
    /// off (local POC) no authorization middleware runs, so no policy metadata is attached.
    /// </summary>
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app, bool enforceRoles)
    {
        var group = app.MapGroup("/api/v1/enrollments")
            .WithTags("Enrollments")
            .RequireRateLimiting("lookup");

        group.MapPost("", async (
                CreateEnrollment.Request request,
                CreateEnrollment.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var dto = await handler.HandleAsync(request, RequestUser.Username(http), cancellationToken);
                return Results.Created($"/api/v1/enrollments/{dto.Id}", dto);
            })
            .AddEndpointFilter<ValidationFilter<CreateEnrollment.Request>>()
            .WithSummary("Create a new enrollment application")
            .Produces<EnrollmentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("", async (
                EnrollmentStatus? status,
                int? page,
                int? pageSize,
                ListEnrollments.Handler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(status, page ?? 1, pageSize ?? 20, cancellationToken);
                return Results.Ok(result);
            })
            .WithSummary("List enrollment applications (paged, newest first)")
            .Produces<PagedResult<EnrollmentSummaryDto>>();

        group.MapGet("{id:guid}", async (
                Guid id,
                GetEnrollment.Handler handler,
                CancellationToken cancellationToken) =>
            {
                var dto = await handler.HandleAsync(id, cancellationToken);
                return dto is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'.")
                    : Results.Ok(dto);
            })
            .WithSummary("Get one enrollment application by id")
            .Produces<EnrollmentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("{id:guid}", async (
                Guid id,
                UpdateEnrollment.Request request,
                UpdateEnrollment.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var dto = await handler.HandleAsync(id, request, RequestUser.Username(http), cancellationToken);
                return dto is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'.")
                    : Results.Ok(dto);
            })
            .AddEndpointFilter<ValidationFilter<UpdateEnrollment.Request>>()
            .WithSummary("Edit an existing enrollment application")
            .Produces<EnrollmentDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var decide = group.MapPost("{id:guid}/decision", async (
                Guid id,
                DecideEnrollment.Request request,
                DecideEnrollment.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var (outcome, enrollment) = await handler.HandleAsync(
                    id, request, RequestUser.Username(http), cancellationToken);
                return outcome switch
                {
                    DecideEnrollment.Outcome.Applied => Results.Ok(enrollment),
                    // Accepted but not yet applied by the (asynchronous) workflow — 202, not a false 200.
                    DecideEnrollment.Outcome.Accepted => Results.Accepted($"/api/v1/enrollments/{id}", enrollment),
                    DecideEnrollment.Outcome.NotFound => Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'."),
                    DecideEnrollment.Outcome.NotAssignee => Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Not your review",
                        detail: "Only the reviewer who claimed this application can approve or reject it."),
                    DecideEnrollment.Outcome.Conflict => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Decided by another reviewer",
                        detail: "Another reviewer decided this application first; your decision was not recorded."),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Enrollment is not under review",
                        detail: "Only an enrollment that is currently under review can be approved or rejected."),
                };
            })
            .AddEndpointFilter<ValidationFilter<DecideEnrollment.Request>>()
            .WithSummary("Approve or reject an enrollment you have claimed (assignee only)")
            .Produces<EnrollmentDto>()
            .Produces<EnrollmentDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        if (enforceRoles)
        {
            decide.RequireAuthorization("CanReview");
        }

        var requestCorrections = group.MapPost("{id:guid}/request-corrections", async (
                Guid id,
                RequestCorrections.Request request,
                RequestCorrections.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var (outcome, enrollment) = await handler.HandleAsync(
                    id, request, RequestUser.Username(http), cancellationToken);
                return outcome switch
                {
                    RequestCorrections.Outcome.Applied => Results.Ok(enrollment),
                    RequestCorrections.Outcome.Accepted => Results.Accepted($"/api/v1/enrollments/{id}", enrollment),
                    RequestCorrections.Outcome.NotFound => Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'."),
                    RequestCorrections.Outcome.NotAssignee => Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Not your review",
                        detail: "Only the reviewer who claimed this application can send it back for corrections."),
                    RequestCorrections.Outcome.Conflict => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Decided by another reviewer",
                        detail: "Another reviewer acted on this application first; your request was not recorded."),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Enrollment is not under review",
                        detail: "Only an enrollment that is currently under review can be sent back for corrections."),
                };
            })
            .AddEndpointFilter<ValidationFilter<RequestCorrections.Request>>()
            .WithSummary("Send an application you have claimed back to the operator for corrections (assignee only)")
            .Produces<EnrollmentDto>()
            .Produces<EnrollmentDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        if (enforceRoles)
        {
            requestCorrections.RequireAuthorization("CanReview");
        }

        // Resubmit a corrected application (operator) — re-enters screening.
        group.MapPost("{id:guid}/resubmit", async (
                Guid id,
                ResubmitEnrollment.Handler handler,
                CancellationToken cancellationToken) =>
            {
                var (outcome, enrollment) = await handler.HandleAsync(id, cancellationToken);
                return outcome switch
                {
                    ResubmitEnrollment.Outcome.Resubmitted => Results.Ok(enrollment),
                    ResubmitEnrollment.Outcome.NotFound => Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'."),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Not awaiting corrections",
                        detail: "Only an application sent back for corrections can be resubmitted."),
                };
            })
            .WithSummary("Resubmit a corrected application for review")
            .Produces<EnrollmentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Withdraw an application before it is decided (operator).
        group.MapPost("{id:guid}/withdraw", async (
                Guid id,
                WithdrawEnrollment.Request request,
                WithdrawEnrollment.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var (outcome, enrollment) = await handler.HandleAsync(
                    id, request, RequestUser.Username(http), cancellationToken);
                return outcome switch
                {
                    WithdrawEnrollment.Outcome.Withdrawn => Results.Ok(enrollment),
                    WithdrawEnrollment.Outcome.Accepted => Results.Accepted($"/api/v1/enrollments/{id}", enrollment),
                    WithdrawEnrollment.Outcome.NotFound => Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'."),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Already concluded",
                        detail: "This application has already been approved, rejected, or withdrawn."),
                };
            })
            .AddEndpointFilter<ValidationFilter<WithdrawEnrollment.Request>>()
            .WithSummary("Withdraw an application before it is decided")
            .Produces<EnrollmentDto>()
            .Produces<EnrollmentDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // --- The reviewer work queue ----------------------------------------------------
        // Ownership lives on the enrollment (status + assignee), so claim/release key off the
        // enrollment id, not a Camunda task key. The list is the whole live pipeline; the SPA
        // groups it into available / mine / with-others.
        var review = app.MapGroup("/api/v1/review-tasks")
            .WithTags("Review tasks")
            .RequireRateLimiting("lookup");
        if (enforceRoles)
        {
            review.RequireAuthorization("CanReview");
        }

        review.MapGet("", async (
                ReviewTasks.ListHandler handler,
                CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(cancellationToken)))
            .WithSummary("List reviews in progress and waiting, oldest first (reviewer role)")
            .Produces<IReadOnlyList<ReviewTasks.ReviewTaskDto>>();

        review.MapPost("{id:guid}/claim", async (
                Guid id,
                ReviewTasks.ClaimHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var isSupervisor = RequestUser.Roles(http).Contains(RequestUser.SupervisorRole);
                var outcome = await handler.HandleAsync(
                    id, RequestUser.Username(http), isSupervisor, cancellationToken);
                return outcome switch
                {
                    ReviewTasks.ClaimOutcome.Claimed => Results.Ok(new { assignee = RequestUser.Username(http) }),
                    ReviewTasks.ClaimOutcome.SupervisorOnly => Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Supervisor review required",
                        detail: "This is a high-risk application; only a supervisor can claim it."),
                    ReviewTasks.ClaimOutcome.Taken => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Already claimed",
                        detail: "Another reviewer claimed this application first, or it is no longer waiting."),
                    _ => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found",
                        detail: $"No enrollment exists with id '{id}'."),
                };
            })
            .WithSummary("Claim a pending review, taking ownership (reviewer role)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        review.MapPost("{id:guid}/release", async (
                Guid id,
                ReviewTasks.ReleaseHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var outcome = await handler.HandleAsync(id, RequestUser.Username(http), cancellationToken);
                return outcome switch
                {
                    ReviewTasks.ReleaseOutcome.Released => Results.NoContent(),
                    ReviewTasks.ReleaseOutcome.NotAssignee => Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Not your review",
                        detail: "Only the reviewer who claimed this application can release it."),
                    _ => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found",
                        detail: $"No enrollment exists with id '{id}'."),
                };
            })
            .WithSummary("Release a claimed review back to the queue (assignee only)")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
