using Farm360.Application.Health.Tasks;
using Farm360.Application.InAppNotifications;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Identity;
using Farm360.Domain.Livestock;
using Farm360.Domain.Tenancy;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <inheritdoc cref="IHealthTaskSource"/>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class HealthTaskSource(ApplicationDbContext context) : IHealthTaskSource
{
    public async Task<IReadOnlyList<HealthTaskRow>> GetPendingAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default)
    {
        var rows = await context.VaccinationEvents
            .AsNoTracking()
            .Join(context.Animals, ve => ve.AnimalId, a => a.Id, (ve, a) => new { Event = ve, Animal = a })
            .Where(x => x.Animal.FarmId == farmId &&
                        AnimalStatusGroups.OnFarm.Contains(x.Animal.Status) &&
                        x.Event.ScheduledDate <= upTo &&
                        (x.Event.Status == VaccinationStatus.Scheduled || x.Event.Status == VaccinationStatus.Overdue))
            .Select(x => new
            {
                x.Event,
                Tag = x.Animal.Tag.TagId,
                x.Animal.FarmId,
                x.Animal.LatestWeightKg,
                Location = x.Animal.Movements
                    .Where(m => m.RemovedAtUtc == null)
                    .Select(m => new { m.ShedId, m.PenId })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new HealthTaskRow(r.Event, r.Tag, r.FarmId, r.Location?.ShedId, r.Location?.PenId, r.LatestWeightKg))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, HealthTaskStockItem>> GetStockItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        if (itemIds.Count == 0)
            return new Dictionary<Guid, HealthTaskStockItem>();

        var ids = itemIds.Distinct().ToList();
        return await context.InventoryItems.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .Select(i => new HealthTaskStockItem(i.Id, i.Name, i.UnitOfMeasure, i.CurrentStock, i.IsActive))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, HealthTaskUnit>> GetUnitsAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitIds);
        if (unitIds.Count == 0)
            return new Dictionary<Guid, HealthTaskUnit>();

        var ids = unitIds.Distinct().ToList();
        return await context.UnitConversions.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new HealthTaskUnit(u.Id, u.Name, u.Factor))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }
}

/// <inheritdoc cref="INotificationStore"/>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class NotificationStore(ApplicationDbContext context) : INotificationStore
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
        await context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public Task<Notification?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

    public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(u => u
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), cancellationToken);
    }

    public async Task<IReadOnlySet<(Guid UserId, string DedupKey)>> GetExistingKeysAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(dedupKeys);
        if (userIds.Count == 0 || dedupKeys.Count == 0)
            return new HashSet<(Guid, string)>();

        var users = userIds.Distinct().ToList();
        var keys = dedupKeys.Distinct().ToList();
        var rows = await context.Notifications.AsNoTracking()
            .Where(n => n.DedupKey != null && keys.Contains(n.DedupKey) && users.Contains(n.UserId))
            .Select(n => new { n.UserId, n.DedupKey })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.UserId, r.DedupKey!)).ToHashSet();
    }

    public void Add(Notification notification) => context.Notifications.Add(notification);
}

/// <inheritdoc cref="IHealthReminderRecipients"/>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class HealthReminderRecipients(ApplicationDbContext context) : IHealthReminderRecipients
{
    /// <summary>Roles that do health and feeding work; Viewer (read-only) is left out.</summary>
    private static readonly Guid[] WorkingRoles =
    [
        SystemRoleIds.Owner, SystemRoleIds.FarmManager, SystemRoleIds.Veterinarian, SystemRoleIds.Worker,
    ];

    public async Task<IReadOnlyList<Guid>> GetUserIdsAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        var branchId = await context.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Select(f => (Guid?)f.BranchId)
            .FirstOrDefaultAsync(cancellationToken);
        if (branchId is null)
            return [];

        var demoViewer = Farm360.Application.Demo.DemoConstants.ViewerUserId;
        // Same farm rule as sign-in: a member tied to a branch (and not the owner) sees only that branch's farms.
        return await context.TenantUsers.AsNoTracking()
            .Where(tu => tu.TenantId == tenantId &&
                         tu.Status == TenantUserStatus.Active &&
                         tu.UserId != demoViewer &&
                         (tu.IsOwner || WorkingRoles.Contains(tu.RoleId)) &&
                         (tu.IsOwner || tu.BranchId == null || tu.BranchId == branchId))
            .Select(tu => tu.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
