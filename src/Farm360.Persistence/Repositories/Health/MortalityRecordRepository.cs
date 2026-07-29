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

    public async Task<(IReadOnlyList<MortalityRecord> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? animalId = null,
        string? reason = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = context.MortalityRecords.AsNoTracking().AsQueryable();

        if (farmId.HasValue)
        {
            var animalIdsInFarm = context.Animals.Where(a => a.FarmId == farmId.Value).Select(a => a.Id);
            query = query.Where(m => animalIdsInFarm.Contains(m.AnimalId));
        }

        if (animalId.HasValue)
        {
            query = query.Where(m => m.AnimalId == animalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            var r = reason.Trim();
            query = query.Where(m => (m.DiseaseName != null && EF.Functions.Like(m.DiseaseName, $"%{r}%")) ||
                                     (m.PostMortemNotes != null && EF.Functions.Like(m.PostMortemNotes, $"%{r}%")));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(m => (m.DiseaseName != null && EF.Functions.Like(m.DiseaseName, $"%{term}%")) ||
                                     (m.PostMortemNotes != null && EF.Functions.Like(m.PostMortemNotes, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("deathdate", false)    => query.OrderBy(m => m.DeathDate),
            ("deathdate", true)     => query.OrderByDescending(m => m.DeathDate),
            ("createdat", false)    => query.OrderBy(m => m.CreatedAtUtc),
            _                       => query.OrderByDescending(m => m.DeathDate)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(MortalityRecord record) => context.MortalityRecords.Add(record);
}
