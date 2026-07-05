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
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app)
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
                var dto = await handler.HandleAsync(request, ResolveOperator(http), cancellationToken);
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
                var dto = await handler.HandleAsync(id, request, ResolveOperator(http), cancellationToken);
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

        group.MapPost("{id:guid}/decision", async (
                Guid id,
                DecideEnrollment.Request request,
                DecideEnrollment.Handler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var (outcome, enrollment) = await handler.HandleAsync(
                    id, request, ResolveOperator(http), cancellationToken);
                return outcome switch
                {
                    DecideEnrollment.Outcome.Applied => Results.Ok(enrollment),
                    // Accepted but not yet applied by the (asynchronous) workflow — 202, not a false 200.
                    DecideEnrollment.Outcome.Accepted => Results.Accepted($"/api/v1/enrollments/{id}", enrollment),
                    DecideEnrollment.Outcome.NotFound => Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Enrollment not found", detail: $"No enrollment exists with id '{id}'."),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Enrollment is not under review",
                        detail: "Only an enrollment that is currently under review can be approved or rejected."),
                };
            })
            .WithSummary("Approve or reject an enrollment that is under review")
            .Produces<EnrollmentDto>()
            .Produces<EnrollmentDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>Authenticated operator's username, or "anonymous" when auth is off.</summary>
    private static string ResolveOperator(HttpContext http) =>
        http.User.FindFirst("preferred_username")?.Value
        ?? http.User.Identity?.Name
        ?? "anonymous";
}
