using Farm360.Domain.Intelligence;
using Farm360.Domain.Intelligence.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Intelligence;

public class InsightRepository : IInsightRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DbSet<ActionableInsight> _dbSet;

    public InsightRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<ActionableInsight>();
    }

    public async Task<ActionableInsight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(ActionableInsight insight, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(insight, cancellationToken);
    }

    public async Task<List<ActionableInsight>> GetActiveInsightsByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.AnimalId == animalId && !x.IsDismissed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActionableInsight>> GetActiveInsightsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.FarmId == farmId && !x.IsDismissed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
