using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Livestock;

/// <summary>
/// EF Core implementation of IAnimalRepository.
/// Constitution §8 (CQRS): Complex queries may use DbContext directly — no mapping overhead.
/// F360-MTA-2026-001: ALL queries automatically scoped by EF Global Query Filter:
///   WHERE TenantId = @currentTenantId AND IsDeleted = 0
///   — this repository adds NO explicit tenant filter. The DbContext handles it.
/// </summary>
public sealed class AnimalRepository(ApplicationDbContext context) : IAnimalRepository
{
    private readonly DbSet<Animal> _animals = context.Animals;

    // ══════════════════════════════════════════════════════════════════════════
    // QUERIES
    // ══════════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<Animal?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        await _animals
            .Include(a => a.WeightRecords)
            .Include(a => a.BreedingRecords)
            .Include(a => a.Photos)
            .Include(a => a.Movements)
            .FirstOrDefaultAsync(a => a.Id == animalId, cancellationToken);

    /// <inheritdoc/>
    public async Task<Animal?> GetByIdWithWeightsAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        await _animals
            .Include(a => a.WeightRecords)
            .Include(a => a.Movements)
            .FirstOrDefaultAsync(a => a.Id == animalId, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TagExistsAsync(
        string tagId,
        Guid? excludeAnimalId = null,
        CancellationToken cancellationToken = default)
    {
        // Normalize the same way AnimalTag.Create does — uppercase trim
        var normalizedTagId = tagId.Trim().ToUpperInvariant();

        var query = _animals.Where(a => a.Tag.TagId == normalizedTagId);

        if (excludeAnimalId.HasValue)
            query = query.Where(a => a.Id != excludeAnimalId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Animal> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? shedId = null,
        AnimalSpecies? species = null,
        AnimalSex? sex = null,
        AnimalStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _animals
            .Include(a => a.Photos.Where(p => p.IsPrimary))
            .Include(a => a.Movements.Where(m => m.RemovedAtUtc == null))
            .AsNoTracking()
            .AsQueryable();

        // ── Filters ───────────────────────────────────────────────────────────
        if (farmId.HasValue)
            query = query.Where(a => a.FarmId == farmId.Value);

        if (shedId.HasValue)
            query = query.Where(a => a.Movements.Any(m => m.RemovedAtUtc == null && m.ShedId == shedId.Value));

        if (species.HasValue)
            query = query.Where(a => a.Species == species.Value);

        if (sex.HasValue)
            query = query.Where(a => a.Sex == sex.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        // ── Full-text search: TagId or BreedName ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = searchTerm.Trim().ToUpperInvariant();
            query = query.Where(a =>
                a.Tag.TagId.Contains(normalized) ||
                a.BreedName.Contains(searchTerm));
        }

        // ── Count (before pagination) ─────────────────────────────────────────
        var totalCount = await query.CountAsync(cancellationToken);

        // ── Sort ──────────────────────────────────────────────────────────────
        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("tagid", false)           => query.OrderBy(a => a.Tag.TagId),
            ("tagid", true)            => query.OrderByDescending(a => a.Tag.TagId),
            ("breed", false)           => query.OrderBy(a => a.BreedName),
            ("breed", true)            => query.OrderByDescending(a => a.BreedName),
            ("dateofbirth", false)     => query.OrderBy(a => a.DateOfBirth),
            ("dateofbirth", true)      => query.OrderByDescending(a => a.DateOfBirth),
            ("weight", false)          => query.OrderBy(a => a.LatestWeightKg),
            ("weight", true)           => query.OrderByDescending(a => a.LatestWeightKg),
            ("acquisitiondate", false) => query.OrderBy(a => a.AcquisitionDate),
            ("acquisitiondate", true)  => query.OrderByDescending(a => a.AcquisitionDate),
            ("createdat", false)       => query.OrderBy(a => a.CreatedAtUtc),
            _                          => query.OrderByDescending(a => a.CreatedAtUtc)
        };

        // ── Pagination ────────────────────────────────────────────────────────
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), totalCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default) =>
        await _animals.CountAsync(
            a => a.Status == AnimalStatus.Active || a.Status == AnimalStatus.Quarantined,
            cancellationToken);

    // ══════════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ══════════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public void Add(Animal animal) =>
        _animals.Add(animal);

    /// <inheritdoc/>
    public void Update(Animal animal) =>
        _animals.Update(animal);

    /// <inheritdoc/>
    /// <remarks>
    /// Sets IsDeleted = true via domain method. The AuditSaveChangesInterceptor
    /// will intercept any EntityState.Deleted and convert to soft-delete automatically,
    /// but calling SoftDelete() directly is the correct pattern per Constitution §12.
    /// </remarks>
    public void SoftDelete(Animal animal, Guid deletedBy) =>
        animal.SoftDelete(deletedBy);
}
