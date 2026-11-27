using Farm360.Domain.Dashboard;
using Farm360.Domain.Dashboard.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Identity;
using Farm360.Domain.Inventory.Enums;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Dashboard;

/// <summary>
/// Plain COUNT queries for the setup checklist. Tenant isolation and soft delete come from the global
/// query filters; the explicit TenantId predicates match the other dashboard repositories.
/// DbContext is not thread-safe, so the queries run one after another.
/// </summary>
public sealed class FarmSetupStatusRepository : IFarmSetupStatusRepository
{
    private readonly ApplicationDbContext _context;

    public FarmSetupStatusRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FarmSetupCounts> GetCountsAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        var hasLocation = await _context.Farms.AsNoTracking()
            .AnyAsync(f => f.TenantId == tenantId && f.Id == farmId && f.Latitude != null && f.Longitude != null, cancellationToken);

        var sheds = await _context.Sheds.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && s.FarmId == farmId, cancellationToken);

        // Breeds are tenant-wide and may include platform-wide reference breeds (TenantId == Guid.Empty),
        // which the query filter already exposes to every tenant.
        var breeds = await _context.Breeds.AsNoTracking().CountAsync(cancellationToken);

        var animals = await _context.Animals.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.FarmId == farmId, cancellationToken);

        var weights = await (from w in _context.WeightRecords.AsNoTracking()
                             join a in _context.Animals.AsNoTracking() on w.AnimalId equals a.Id
                             where a.TenantId == tenantId && a.FarmId == farmId
                             select w.Id).CountAsync(cancellationToken);

        var stockedItems = _context.InventoryItems.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.FarmId == farmId && i.IsActive && i.CurrentStock > 0);
        var inventoryInStock = await stockedItems.CountAsync(cancellationToken);
        var feedInStock = await stockedItems.CountAsync(i => i.Category == InventoryCategory.Feed, cancellationToken);

        var recipes = await _context.FeedFormulas.AsNoTracking()
            .CountAsync(f => f.TenantId == tenantId && f.Status != FormulaStatus.Archived, cancellationToken);

        var ruleSets = await _context.FeedingRuleSets.AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && r.IsActive, cancellationToken);

        var activePlans = await _context.AnimalFeedingPlans.AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.FarmId == farmId && p.Status == FeedingPlanStatus.Active, cancellationToken);

        var accountingSaved = await _context.FarmAccountingSettings.AsNoTracking()
            .AnyAsync(s => s.TenantId == tenantId && s.FarmId == farmId, cancellationToken);

        var members = await _context.TenantUsers.AsNoTracking()
            .CountAsync(u => u.TenantId == tenantId && u.Status != TenantUserStatus.Deactivated, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var pendingInvitations = await _context.Invitations.AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending && i.ExpiresAtUtc > nowUtc, cancellationToken);

        return new FarmSetupCounts(
            sheds,
            breeds,
            animals,
            weights,
            inventoryInStock,
            feedInStock,
            recipes,
            ruleSets,
            activePlans,
            accountingSaved,
            members,
            pendingInvitations,
            hasLocation);
    }
}
