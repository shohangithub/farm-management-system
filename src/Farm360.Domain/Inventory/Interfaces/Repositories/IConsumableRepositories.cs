using Farm360.Domain.Inventory.Enums;

namespace Farm360.Domain.Inventory.Interfaces.Repositories;

public interface IConsumableUsagePlanRepository
{
    Task<ConsumableUsagePlan?> GetByIdAsync(Guid id, bool includeItems = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumableUsagePlan>> GetByFarmIdAsync(Guid farmId, bool includeItems = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumableUsagePlan>> GetActivePlansAsync(Guid farmId, DateOnly? onDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsumableUsagePlan>> GetAllActivePlansAcrossTenantsAsync(DateOnly onDate, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ConsumableUsagePlan> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid farmId,
        ConsumableUsagePlanStatus? status = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
    Task AddAsync(ConsumableUsagePlan plan, CancellationToken cancellationToken = default);
    void Update(ConsumableUsagePlan plan);
    void Delete(ConsumableUsagePlan plan);
}

public interface IDailyConsumableEntryRepository
{
    Task<DailyConsumableEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyConsumableEntry>> GetByDateAsync(Guid farmId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyConsumableEntry>> GetByDateRangeAsync(Guid farmId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<HashSet<(Guid PlanId, Guid PlanItemId)>> GetExistingEntryPlanItemIdsByDateAsync(Guid farmId, DateOnly date, CancellationToken cancellationToken = default);
    Task<HashSet<(Guid PlanId, Guid PlanItemId)>> GetExistingEntryPlanItemIdsAcrossTenantsByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task AddAsync(DailyConsumableEntry entry, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<DailyConsumableEntry> entries, CancellationToken cancellationToken = default);
    void Update(DailyConsumableEntry entry);
}
