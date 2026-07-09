using Farm360.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Farm360.Infrastructure.Messaging;

/// <summary>
/// SignalR real-time notification service.
/// F360-MTA-2026-001 §6 (Layer 6): All SignalR groups are tenant-scoped.
/// Group naming: {tenantId}:user:{userId} or {tenantId}:all
/// Cross-tenant SignalR events are impossible by design.
/// </summary>
public sealed class SignalRNotificationService(IHubContext<FarmNotificationHub> hubContext)
    : INotificationService
{
    public async Task SendToUserAsync(
        Guid tenantId, Guid userId, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = $"{tenantId}:user:{userId}";
        await hubContext.Clients.Group(groupName)
            .SendAsync(eventType, payload, cancellationToken);
    }

    public async Task SendToTenantAsync(
        Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var groupName = $"{tenantId}:all";
        await hubContext.Clients.Group(groupName)
            .SendAsync(eventType, payload, cancellationToken);
    }
}

/// <summary>
/// SignalR Hub for farm real-time events.
/// F360-MTA-2026-001: Hub enforces tenant-scoped group membership.
/// Clients subscribe to their own tenant groups only.
/// </summary>
public sealed class FarmNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantId}:all");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantId}:user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception) =>
        base.OnDisconnectedAsync(exception);
}
