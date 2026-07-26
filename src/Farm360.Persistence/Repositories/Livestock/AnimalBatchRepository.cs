using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Livestock;

public sealed class AnimalBatchRepository(ApplicationDbContext context) : IAnimalBatchRepository
{
    public async Task<AnimalBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.AnimalBatches
            .Include(b => b.Animals)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task AddAsync(AnimalBatch batch, CancellationToken cancellationToken = default)
    {
        await context.AnimalBatches.AddAsync(batch, cancellationToken);
    }

    public async Task<(IReadOnlyList<AnimalBatch> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid farmId,
        BatchStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = context.AnimalBatches
            .Include(b => b.Animals)
            .Where(b => b.FarmId == farmId);

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
