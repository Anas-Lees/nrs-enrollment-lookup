namespace Nrs.Api.Features.Notifications;

/// <summary>
/// Minimal-API routes for the staff notification bell, under <c>/api/v1/notifications</c>.
/// Covered by the operator fallback policy when auth is on (any signed-in staff member has
/// a bell); each query is scoped to the caller's own username + roles.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        group.MapGet("", async (
                bool? unreadOnly,
                int? limit,
                NotificationsFeature.ListHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    RequestUser.Username(http), RequestUser.Roles(http),
                    unreadOnly ?? false, limit ?? 20, cancellationToken);
                return Results.Ok(result);
            })
            .WithSummary("List the caller's notifications (newest first) with the unread count")
            .Produces<NotificationsFeature.NotificationListDto>();

        group.MapPost("{id:guid}/read", async (
                Guid id,
                NotificationsFeature.MarkReadHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var found = await handler.HandleAsync(
                    id, RequestUser.Username(http), RequestUser.Roles(http), cancellationToken);
                return found
                    ? Results.NoContent()
                    : Results.Problem(statusCode: StatusCodes.Status404NotFound,
                        title: "Notification not found",
                        detail: "No notification with that id is addressed to you.");
            })
            .WithSummary("Mark one notification as read")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("read-all", async (
                NotificationsFeature.MarkReadHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var count = await handler.HandleAllAsync(
                    RequestUser.Username(http), RequestUser.Roles(http), cancellationToken);
                return Results.Ok(new { marked = count });
            })
            .WithSummary("Mark all of the caller's notifications as read");

        return app;
    }
}
