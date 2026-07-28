using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class MortalityRecordRepository(ApplicationDbContext context) : IMortalityRecordRepository
{
    public async Task<bool> ExistsByAnimalIdAsync(Guid animalId, CancellationToken ct = default)
    {
        return await context.MortalityRecords.AnyAsync(m => m.AnimalId == animalId, ct);
    }
    public async Task<(IReadOnlyList<MortalityRecord> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = context.MortalityRecords.AsQueryable();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.DeathDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(MortalityRecord record) => context.MortalityRecords.Add(record);
}
