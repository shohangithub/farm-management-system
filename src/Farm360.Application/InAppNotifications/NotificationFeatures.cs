using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Jobs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.InAppNotifications;

// ══════════════════════════════════════════════════════════════════════════════
// The bell: list, mark read
// ══════════════════════════════════════════════════════════════════════════════

public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

/// <summary>The signed-in user's latest notifications in this tenant.</summary>
public sealed record GetMyNotificationsQuery(int Take = 30) : IRequest<NotificationListDto>;

internal sealed class GetMyNotificationsQueryHandler(INotificationStore store, ICurrentUserService currentUser)
    : IRequestHandler<GetMyNotificationsQuery, NotificationListDto>
{
    public async Task<NotificationListDto> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return new NotificationListDto([], 0);

        var take = Math.Clamp(request.Take, 1, 100);
        var items = await store.GetForUserAsync(userId, take, cancellationToken);
        var unread = await store.CountUnreadAsync(userId, cancellationToken);
        return new NotificationListDto(items.Select(NotificationDto.From).ToList(), unread);
    }
}

public sealed record MarkNotificationReadCommand(Guid Id) : IRequest;

internal sealed class MarkNotificationReadCommandHandler(INotificationStore store, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        // Only your own: another user's id is simply "not found".
        var notification = await store.GetForUserByIdAsync(userId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Notification not found.");
        notification.MarkAsRead();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record MarkAllNotificationsReadCommand : IRequest<int>;

internal sealed class MarkAllNotificationsReadCommandHandler(INotificationStore store, ICurrentUserService currentUser)
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    public Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return store.MarkAllReadAsync(userId, cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Daily health reminders (Hangfire)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Hourly job: for every tenant, each farm whose local clock has reached
/// <see cref="GenerateTenantHealthRemindersCommand.ReminderHour"/> (7:00) gets its reminders for that
/// day once. Running hourly lets every farm get them at 7:00 in its own time zone; later runs the
/// same day create nothing (one reminder per user, kind and day).
/// </summary>
public sealed record GenerateHealthRemindersCommand : IRequest<TenantJobSummary>;

internal sealed class GenerateHealthRemindersCommandHandler(ITenantJobRunner runner)
    : IRequestHandler<GenerateHealthRemindersCommand, TenantJobSummary>
{
    public Task<TenantJobSummary> Handle(GenerateHealthRemindersCommand request, CancellationToken cancellationToken) =>
        runner.ForEachTenantAsync("health-reminders",
            (scope, ct) => scope.Sender.Send(new GenerateTenantHealthRemindersCommand(scope.TenantId), ct),
            cancellationToken);
}

/// <summary>One tenant's reminders (the ambient tenant must already be set). Returns how many were created.</summary>
public sealed record GenerateTenantHealthRemindersCommand(Guid TenantId, bool IgnoreReminderHour = false) : IRequest<int>
{
    public const int ReminderHour = 7;
}

internal sealed class GenerateTenantHealthRemindersCommandHandler(
    IFarmRepository farms,
    IHealthReminderService reminders,
    ITenantService tenantService,
    IFarmClock? clock = null) : IRequestHandler<GenerateTenantHealthRemindersCommand, int>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<int> Handle(GenerateTenantHealthRemindersCommand request, CancellationToken cancellationToken)
    {
        // The public demo farm is rebuilt weekly and visited anonymously: no reminders.
        if (Farm360.Application.Demo.DemoConstants.IsDemoSlug(tenantService.TenantSlug))
            return 0;

        var created = 0;
        foreach (var farm in await farms.GetAllByTenantAsync(request.TenantId, cancellationToken))
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, _clock.ZoneFor(request.TenantId, farm.Id));
            if (!request.IgnoreReminderHour && local.Hour < GenerateTenantHealthRemindersCommand.ReminderHour)
                continue;

            created += (await reminders.GenerateForFarmAsync(request.TenantId, farm.Id, DateOnly.FromDateTime(local), cancellationToken)).Count;
        }

        return created;
    }
}
