using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Feeding;

public sealed class FeedIngredientRepository : IFeedIngredientRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedIngredientRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedIngredient?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedIngredient>()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IReadOnlyList<FeedIngredient>> GetAllAsync(Guid tenantId, bool includePreloaded = true, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedIngredient>()
            .Where(i => i.TenantId == tenantId || (includePreloaded && i.IsPreloaded))
            .OrderBy(i => i.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(FeedIngredient ingredient, CancellationToken ct = default)
    {
        await _dbContext.Set<FeedIngredient>().AddAsync(ingredient, ct);
    }

    public void Update(FeedIngredient ingredient)
    {
        _dbContext.Set<FeedIngredient>().Update(ingredient);
    }

    public void Delete(FeedIngredient ingredient)
    {
        _dbContext.Set<FeedIngredient>().Remove(ingredient);
    }
}

public sealed class FeedFormulaRepository : IFeedFormulaRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedFormulaRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedFormula?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedFormula>()
            .Include(f => f.Ingredients)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<FeedFormula>> GetListAsync(Guid tenantId, int pageNumber, int pageSize, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _dbContext.Set<FeedFormula>()
            .Include(f => f.Ingredients)
            .Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(f => EF.Functions.Like(f.Title, $"%{searchTerm}%"));
        }

        return await query
            .OrderByDescending(f => f.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _dbContext.Set<FeedFormula>()
            .Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(f => EF.Functions.Like(f.Title, $"%{searchTerm}%"));
        }

        return await query.CountAsync(ct);
    }

    public async Task AddAsync(FeedFormula formula, CancellationToken ct = default)
    {
        await _dbContext.Set<FeedFormula>().AddAsync(formula, ct);
    }

    public void Update(FeedFormula formula)
    {
        _dbContext.Set<FeedFormula>().Update(formula);
    }

    public void Delete(FeedFormula formula)
    {
        _dbContext.Set<FeedFormula>().Remove(formula);
    }
}

public sealed class FeedingScheduleRepository : IFeedingScheduleRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedingScheduleRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedingSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedingSchedule>()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<FeedingSchedule>> GetListByFarmAsync(Guid tenantId, Guid farmId, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedingSchedule>()
            .Where(s => s.TenantId == tenantId && s.FarmId == farmId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(FeedingSchedule schedule, CancellationToken ct = default)
    {
        await _dbContext.Set<FeedingSchedule>().AddAsync(schedule, ct);
    }

    public void Update(FeedingSchedule schedule)
    {
        _dbContext.Set<FeedingSchedule>().Update(schedule);
    }

    public void Delete(FeedingSchedule schedule)
    {
        _dbContext.Set<FeedingSchedule>().Remove(schedule);
    }
}

public sealed class FeedConsumptionLogRepository : IFeedConsumptionLogRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FeedConsumptionLogRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedConsumptionLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Set<FeedConsumptionLog>()
            .Include(l => l.Details)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<IReadOnlyList<FeedConsumptionLog>> GetLogsAsync(Guid tenantId, Guid farmId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var query = _dbContext.Set<FeedConsumptionLog>()
            .Include(l => l.Details)
            .Where(l => l.TenantId == tenantId && l.FarmId == farmId);

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.LogDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(l => l.LogDate <= toDate.Value);
        }

        return await query
            .OrderByDescending(l => l.LogDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(FeedConsumptionLog log, CancellationToken ct = default)
    {
        await _dbContext.Set<FeedConsumptionLog>().AddAsync(log, ct);
    }
}
