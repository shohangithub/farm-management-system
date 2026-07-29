using Farm360.Domain.Intelligence;
using Farm360.Domain.Intelligence.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Intelligence;

public class PerformanceTargetRepository : IPerformanceTargetRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DbSet<PerformanceTarget> _dbSet;

    public PerformanceTargetRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<PerformanceTarget>();
    }

    public async Task<PerformanceTarget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(PerformanceTarget target, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(target, cancellationToken);
    }

    public async Task<PerformanceTarget?> GetTargetForBreedAndStageAsync(string breedName, string stage, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x => x.BreedName == breedName && x.Stage == stage, cancellationToken);
    }
}
