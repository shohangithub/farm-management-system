using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Inventory;

public sealed class ConsumableUsagePlanRepository : IConsumableUsagePlanRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public ConsumableUsagePlanRepository(ApplicationDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<ConsumableUsagePlan?> GetByIdAsync(Guid id, bool includeItems = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ConsumableUsagePlans
            .Where(x => x.Id == id && x.TenantId == _tenantService.TenantId);

        if (includeItems)
        {
            query = query.Include(x => x.Items);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsumableUsagePlan>> GetByFarmIdAsync(Guid farmId, bool includeItems = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ConsumableUsagePlans
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId);

        if (includeItems)
        {
            query = query.Include(x => x.Items);
        }

        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsumableUsagePlan>> GetActivePlansAsync(Guid farmId, DateOnly? onDate = null, CancellationToken cancellationToken = default)
    {
        var targetDate = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return await _dbContext.ConsumableUsagePlans
            .Include(x => x.Items)
            .Where(x => x.FarmId == farmId 
                     && x.TenantId == _tenantService.TenantId
                     && x.Status == ConsumableUsagePlanStatus.Active
                     && x.StartDate <= targetDate
                     && (!x.EndDate.HasValue || x.EndDate.Value >= targetDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsumableUsagePlan>> GetAllActivePlansAcrossTenantsAsync(DateOnly onDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConsumableUsagePlans
            .IgnoreQueryFilters()
            .Include(x => x.Items)
            .Where(x => !x.IsDeleted
                     && x.Status == ConsumableUsagePlanStatus.Active
                     && x.StartDate <= onDate
                     && (!x.EndDate.HasValue || x.EndDate.Value >= onDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ConsumableUsagePlan> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid farmId,
        ConsumableUsagePlanStatus? status = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ConsumableUsagePlans
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%") || (x.Description != null && EF.Functions.Like(x.Description, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(ConsumableUsagePlan plan, CancellationToken cancellationToken = default)
    {
        await _dbContext.ConsumableUsagePlans.AddAsync(plan, cancellationToken);
    }

    public void Update(ConsumableUsagePlan plan)
    {
        _dbContext.ConsumableUsagePlans.Update(plan);
    }

    public void Delete(ConsumableUsagePlan plan)
    {
        _dbContext.ConsumableUsagePlans.Remove(plan);
    }
}

public sealed class DailyConsumableEntryRepository : IDailyConsumableEntryRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public DailyConsumableEntryRepository(ApplicationDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<DailyConsumableEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyConsumableEntries
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenantService.TenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<DailyConsumableEntry>> GetByDateAsync(Guid farmId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyConsumableEntries
            .AsNoTracking()
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId && x.EntryDate == date)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyConsumableEntry>> GetByDateRangeAsync(Guid farmId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DailyConsumableEntries
            .AsNoTracking()
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId && x.EntryDate >= fromDate && x.EntryDate <= toDate)
            .OrderByDescending(x => x.EntryDate)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<(Guid PlanId, Guid PlanItemId)>> GetExistingEntryPlanItemIdsByDateAsync(Guid farmId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entries = await _dbContext.DailyConsumableEntries
            .AsNoTracking()
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId && x.EntryDate == date)
            .Select(x => new { x.ConsumableUsagePlanId, x.ConsumableUsagePlanItemId })
            .ToListAsync(cancellationToken);

        return entries.Select(x => (x.ConsumableUsagePlanId, x.ConsumableUsagePlanItemId)).ToHashSet();
    }

    public async Task<HashSet<(Guid PlanId, Guid PlanItemId)>> GetExistingEntryPlanItemIdsAcrossTenantsByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var entries = await _dbContext.DailyConsumableEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.EntryDate == date)
            .Select(x => new { x.ConsumableUsagePlanId, x.ConsumableUsagePlanItemId })
            .ToListAsync(cancellationToken);

        return entries.Select(x => (x.ConsumableUsagePlanId, x.ConsumableUsagePlanItemId)).ToHashSet();
    }

    public async Task AddAsync(DailyConsumableEntry entry, CancellationToken cancellationToken = default)
    {
        await _dbContext.DailyConsumableEntries.AddAsync(entry, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<DailyConsumableEntry> entries, CancellationToken cancellationToken = default)
    {
        await _dbContext.DailyConsumableEntries.AddRangeAsync(entries, cancellationToken);
    }

    public void Update(DailyConsumableEntry entry)
    {
        _dbContext.DailyConsumableEntries.Update(entry);
    }
}
