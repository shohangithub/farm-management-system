using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Feeding;

public sealed class FeedingRuleSetRepository : IFeedingRuleSetRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedingRuleSetRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedingRuleSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingRuleSets.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<FeedingRuleSet?> GetByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingRuleSets.IgnoreQueryFilters().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedingRuleSet>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingRuleSets.Include(x => x.Lines).AsNoTracking().ToListAsync(cancellationToken);

    public async Task AddAsync(FeedingRuleSet entity, CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingRuleSets.AddAsync(entity, cancellationToken);

    public void Update(FeedingRuleSet entity) =>
        _dbContext.FeedingRuleSets.Update(entity);

    public async Task<FeedingRuleSet?> GetActiveRuleSetAsync(Guid tenantId, Farm360.Domain.Feeding.Enums.TargetAnimalType species, Farm360.Domain.Feeding.Enums.FeedingPurpose purpose, CancellationToken cancellationToken)
    {
        return await _dbContext.FeedingRuleSets
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.Species == species && x.Purpose == purpose && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class AnimalFeedingPlanRepository : IAnimalFeedingPlanRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AnimalFeedingPlanRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnimalFeedingPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.AnimalFeedingPlans.FindAsync([id], cancellationToken);

    public async Task AddAsync(AnimalFeedingPlan entity, CancellationToken cancellationToken = default) =>
        await _dbContext.AnimalFeedingPlans.AddAsync(entity, cancellationToken);

    public void Update(AnimalFeedingPlan entity) =>
        _dbContext.AnimalFeedingPlans.Update(entity);

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetActivePlansByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken)
    {
        return await _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => x.TenantId == tenantId && x.FarmId == farmId && x.Status == Farm360.Domain.Feeding.Enums.FeedingPlanStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetPlansByFarmAsync(Guid tenantId, Guid farmId, Farm360.Domain.Feeding.Enums.FeedingPlanStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => x.TenantId == tenantId && x.FarmId == farmId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetActivePlansAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => x.TenantId == tenantId && x.Status == Farm360.Domain.Feeding.Enums.FeedingPlanStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetAllActivePlansAcrossTenantsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AnimalFeedingPlans
            .IgnoreQueryFilters()
            .Include(x => x.Exclusions)
            .Where(x => x.Status == Farm360.Domain.Feeding.Enums.FeedingPlanStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetActivePlansForAnimalAsync(Guid tenantId, Guid animalId, CancellationToken cancellationToken)
    {
        return await _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => x.TenantId == tenantId && x.AnimalId == animalId && x.Status == Farm360.Domain.Feeding.Enums.FeedingPlanStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetAllPlansForAnimalAsync(Guid tenantId, Guid animalId, CancellationToken cancellationToken)
    {
        return await _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => x.TenantId == tenantId && x.AnimalId == animalId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalFeedingPlan>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return Array.Empty<AnimalFeedingPlan>();

        return await _dbContext.AnimalFeedingPlans
            .Include(x => x.Exclusions)
            .Where(x => idList.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }
}

public sealed class DailyFeedingEntryRepository : IDailyFeedingEntryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DailyFeedingEntryRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DailyFeedingEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.DailyFeedingEntries.FindAsync([id], cancellationToken);

    public async Task AddAsync(DailyFeedingEntry entity, CancellationToken cancellationToken = default) =>
        await _dbContext.DailyFeedingEntries.AddAsync(entity, cancellationToken);

    public void Update(DailyFeedingEntry entity) =>
        _dbContext.DailyFeedingEntries.Update(entity);

    public async Task<IReadOnlyList<DailyFeedingEntry>> GetEntriesByDateAsync(Guid tenantId, Guid farmId, DateOnly date, CancellationToken cancellationToken)
    {
        var query = _dbContext.DailyFeedingEntries
            .Where(x => x.TenantId == tenantId && x.EntryDate == date);

        if (farmId != Guid.Empty)
        {
            query = query.Where(x => x.FarmId == farmId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyFeedingEntry>> GetEntriesByAnimalIdAsync(Guid tenantId, Guid animalId, CancellationToken cancellationToken)
    {
        var planIds = await _dbContext.AnimalFeedingPlans
            .Where(p => p.TenantId == tenantId && p.AnimalId == animalId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (planIds.Count == 0)
            return Array.Empty<DailyFeedingEntry>();

        return await _dbContext.DailyFeedingEntries
            .Where(e => e.TenantId == tenantId && planIds.Contains(e.FeedingPlanId))
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<(Guid PlanId, Guid? RuleLineId)>> GetEntryPlanIdsAcrossTenantsByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var entries = await _dbContext.DailyFeedingEntries
            .IgnoreQueryFilters()
            .Where(x => x.EntryDate == date)
            .Select(x => new { x.FeedingPlanId, x.RuleLineId })
            .ToListAsync(cancellationToken);

        return new HashSet<(Guid PlanId, Guid? RuleLineId)>(entries.Select(x => (x.FeedingPlanId, x.RuleLineId)));
    }
}

public sealed class FeedingReconciliationRepository : IFeedingReconciliationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedingReconciliationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedingCycleReconciliation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingCycleReconciliations.FindAsync([id], cancellationToken);

    public async Task AddAsync(FeedingCycleReconciliation entity, CancellationToken cancellationToken = default) =>
        await _dbContext.FeedingCycleReconciliations.AddAsync(entity, cancellationToken);

    public void Update(FeedingCycleReconciliation entity) =>
        _dbContext.FeedingCycleReconciliations.Update(entity);

    public async Task<FeedingCycleReconciliation?> GetReconciliationByPeriodAsync(Guid tenantId, Guid farmId, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        return await _dbContext.FeedingCycleReconciliations
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.FarmId == farmId && x.PeriodStart == start && x.PeriodEnd == end)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
