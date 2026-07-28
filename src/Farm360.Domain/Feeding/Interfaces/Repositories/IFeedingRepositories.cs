namespace Farm360.Domain.Feeding.Interfaces.Repositories;

public interface IFeedIngredientRepository
{
    Task<FeedIngredient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FeedIngredient>> GetAllAsync(Guid tenantId, bool includePreloaded = true, CancellationToken ct = default);
    Task AddAsync(FeedIngredient ingredient, CancellationToken ct = default);
    void Update(FeedIngredient ingredient);
    void Delete(FeedIngredient ingredient);
}

public interface IFeedFormulaRepository
{
    Task<FeedFormula?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FeedFormula>> GetListAsync(Guid tenantId, int pageNumber, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<int> GetCountAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default);
    Task AddAsync(FeedFormula formula, CancellationToken ct = default);
    void Update(FeedFormula formula);
    void Delete(FeedFormula formula);
}

public interface IFeedingScheduleRepository
{
    Task<FeedingSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FeedingSchedule>> GetListByFarmAsync(Guid tenantId, Guid farmId, CancellationToken ct = default);
    Task AddAsync(FeedingSchedule schedule, CancellationToken ct = default);
    void Update(FeedingSchedule schedule);
    void Delete(FeedingSchedule schedule);
}

public interface IFeedConsumptionLogRepository
{
    Task<FeedConsumptionLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FeedConsumptionLog>> GetLogsAsync(Guid tenantId, Guid farmId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);
    Task AddAsync(FeedConsumptionLog log, CancellationToken ct = default);
}
