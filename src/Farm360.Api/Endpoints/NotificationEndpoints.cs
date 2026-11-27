using Farm360.Application.InAppNotifications;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints;

/// <summary>
/// The header bell: the signed-in user's stored notifications (health reminders) in this tenant.
/// Titles/bodies are English dictionary keys with parameters; the web translates them.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] int? take,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyNotificationsQuery(take ?? 30), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.Notifications.View}")
        .WithSummary("My latest notifications and the unread count");

        group.MapPost("/{id:guid}/read", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new MarkNotificationReadCommand(id), ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.Notifications.View}")
        .WithSummary("Mark one of my notifications as read");

        group.MapPost("/read-all", async (
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var count = await sender.Send(new MarkAllNotificationsReadCommand(), ct);
            return Results.Ok(new { Count = count });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.Notifications.View}")
        .WithSummary("Mark all my notifications as read");

        return app;
    }
}
