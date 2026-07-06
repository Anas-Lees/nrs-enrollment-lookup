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
            .WithSummary("Approve or reject an enrollment that is under review (reviewer role)")
            .Produces<EnrollmentDto>()
            .Produces<EnrollmentDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        if (enforceRoles)
        {
            decide.RequireAuthorization("CanReview");
        }

        // --- The reviewer work queue (Camunda user tasks) -------------------------------
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
            .WithSummary("List open review tasks, oldest first (reviewer role)")
            .Produces<IReadOnlyList<ReviewTasks.ReviewTaskDto>>();

        review.MapPost("{userTaskKey:long}/claim", async (
                long userTaskKey,
                ReviewTasks.ClaimHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var outcome = await handler.HandleAsync(
                    userTaskKey, RequestUser.Username(http), cancellationToken);
                return outcome switch
                {
                    ReviewTasks.ClaimOutcome.Claimed => Results.Ok(new { assignee = RequestUser.Username(http) }),
                    ReviewTasks.ClaimOutcome.Taken => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Task already claimed",
                        detail: "Another reviewer claimed this task first, or it no longer exists."),
                    _ => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Task not found",
                        detail: "No workflow engine is configured."),
                };
            })
            .WithSummary("Claim a review task (reviewer role)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
