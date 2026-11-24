using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Livestock;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <summary>
/// Paged animal list with the presence filter (on farm / left farm). Mirrors the filters and
/// sorts of <c>AnimalRepository.GetPagedAsync</c>; kept separate so the list page and the animal
/// pickers can ask for "on farm only" (Active + Quarantined, <see cref="AnimalStatusGroups"/>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class AnimalListQueries(ApplicationDbContext context) : IAnimalListQueries
{
    public async Task<(IReadOnlyList<Animal> Items, int TotalCount)> GetPagedAsync(
        GetAnimalListQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = context.Animals
            .Include(a => a.Photos.Where(p => p.IsPrimary))
            .Include(a => a.Movements.Where(m => m.RemovedAtUtc == null))
            .AsNoTracking()
            .AsQueryable();

        query = filter.Presence switch
        {
            AnimalPresence.OnFarm => query.Where(a => AnimalStatusGroups.OnFarm.Contains(a.Status)),
            AnimalPresence.LeftFarm => query.Where(a => AnimalStatusGroups.LeftFarm.Contains(a.Status)),
            _ => query
        };

        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.FarmId.HasValue)
            query = query.Where(a => a.FarmId == filter.FarmId.Value);

        if (filter.BatchId.HasValue)
            query = query.Where(a => a.BatchId == filter.BatchId.Value);

        if (filter.ShedId.HasValue)
            query = query.Where(a => a.Movements.Any(m => m.RemovedAtUtc == null && m.ShedId == filter.ShedId.Value));

        if (filter.PenId.HasValue)
            query = query.Where(a => a.Movements.Any(m => m.RemovedAtUtc == null && m.PenId == filter.PenId.Value));

        if (filter.Species.HasValue)
            query = query.Where(a => a.Species == filter.Species.Value);

        if (filter.Sex.HasValue)
            query = query.Where(a => a.Sex == filter.Sex.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            var normalized = term.ToUpperInvariant();
            var breedPattern = $"%{term}%";
            var breeds = context.Breeds;
            // Tag IDs are stored upper-case; breed names match via LIKE (case-insensitive collation).
            query = query.Where(a =>
                a.Tag.TagId.Contains(normalized) ||
                breeds.Any(b => b.Id == a.BreedId && EF.Functions.Like(b.Name, breedPattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (filter.SortBy?.ToLowerInvariant(), filter.SortDescending) switch
        {
            ("tagid", false)           => query.OrderBy(a => a.Tag.TagId),
            ("tagid", true)            => query.OrderByDescending(a => a.Tag.TagId),
            ("breed", false)           => query.OrderBy(a => a.BreedId),
            ("breed", true)            => query.OrderByDescending(a => a.BreedId),
            ("dateofbirth", false)     => query.OrderBy(a => a.DateOfBirth),
            ("dateofbirth", true)      => query.OrderByDescending(a => a.DateOfBirth),
            ("weight", false)          => query.OrderBy(a => a.LatestWeightKg),
            ("weight", true)           => query.OrderByDescending(a => a.LatestWeightKg),
            ("acquisitiondate", false) => query.OrderBy(a => a.AcquisitionDate),
            ("acquisitiondate", true)  => query.OrderByDescending(a => a.AcquisitionDate),
            ("createdat", false)       => query.OrderBy(a => a.CreatedAtUtc),
            _                          => query.OrderByDescending(a => a.CreatedAtUtc)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), totalCount);
    }
}
