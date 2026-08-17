using Farm360.Domain.Interfaces.Repositories;

namespace Farm360.Domain.Feeding.Interfaces.Repositories;

public interface IFeedingRuleSetRepository
{
    Task<FeedingRuleSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedingRuleSet>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(FeedingRuleSet entity, CancellationToken cancellationToken = default);
    void Update(FeedingRuleSet entity);
    Task<FeedingRuleSet?> GetActiveRuleSetAsync(Guid tenantId, Enums.TargetAnimalType species, Enums.FeedingPurpose purpose, CancellationToken cancellationToken);
}

public interface IAnimalFeedingPlanRepository
{
    Task<AnimalFeedingPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AnimalFeedingPlan entity, CancellationToken cancellationToken = default);
    void Update(AnimalFeedingPlan entity);
    Task<IReadOnlyList<AnimalFeedingPlan>> GetActivePlansByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnimalFeedingPlan>> GetActivePlansAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AnimalFeedingPlan?> GetActivePlanForAnimalAsync(Guid tenantId, Guid animalId, CancellationToken cancellationToken);
}

public interface IDailyFeedingEntryRepository
{
    Task<DailyFeedingEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(DailyFeedingEntry entity, CancellationToken cancellationToken = default);
    void Update(DailyFeedingEntry entity);
    Task<IReadOnlyList<DailyFeedingEntry>> GetEntriesByDateAsync(Guid tenantId, Guid farmId, DateOnly date, CancellationToken cancellationToken);
}

public interface IFeedingReconciliationRepository
{
    Task<FeedingCycleReconciliation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(FeedingCycleReconciliation entity, CancellationToken cancellationToken = default);
    void Update(FeedingCycleReconciliation entity);
    Task<FeedingCycleReconciliation?> GetReconciliationByPeriodAsync(Guid tenantId, Guid farmId, DateOnly start, DateOnly end, CancellationToken cancellationToken);
}
