using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Inventory;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Feeding;

public sealed class AnimalFeedAllocationRepository : IAnimalFeedAllocationRepository
{
    private readonly ApplicationDbContext _context;

    public AnimalFeedAllocationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void Add(AnimalFeedAllocation allocation) => _context.AnimalFeedAllocations.Add(allocation);

    public void AddRange(IEnumerable<AnimalFeedAllocation> allocations) =>
        _context.AnimalFeedAllocations.AddRange(allocations);

    public async Task<IReadOnlyList<AnimalFeedAllocation>> GetByAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _context.AnimalFeedAllocations
            .AsNoTracking()
            .Where(a => a.AnimalId == animalId && a.EntryDate >= from && a.EntryDate <= to)
            .OrderBy(a => a.EntryDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AnimalFeedAllocation>> GetByFarmAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AnimalFeedAllocations
            .AsNoTracking()
            .Where(a => a.FarmId == farmId && a.EntryDate >= from && a.EntryDate <= to);

        if (animalId.HasValue && animalId.Value != Guid.Empty)
            query = query.Where(a => a.AnimalId == animalId.Value);

        if (batchId.HasValue && batchId.Value != Guid.Empty)
            query = query.Where(a => a.BatchId == batchId.Value);

        if (shedId.HasValue && shedId.Value != Guid.Empty)
            query = query.Where(a => a.ShedId == shedId.Value);

        return await query
            .OrderBy(a => a.EntryDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Allocation rows for feed actually eaten: their entry was confirmed or adjusted. Rows are
    /// written when an entry is generated (Pending, carrying the planned feed); a pending or skipped
    /// entry fed nothing, so its rows never count. Every allocation hangs off an entry (manual and
    /// direct feeding create a confirmed entry too), so the join loses nothing.
    /// </summary>
    private IQueryable<AnimalFeedAllocation> Eaten() =>
        from a in _context.AnimalFeedAllocations.AsNoTracking()
        join e in _context.DailyFeedingEntries on a.DailyFeedingEntryId equals e.Id
        where e.Status == DailyFeedingEntryStatus.Confirmed || e.Status == DailyFeedingEntryStatus.Adjusted
        select a;

    /// <summary>
    /// Recipes whose quantity is not kilograms: a stock item fed as it is (one ingredient linked to
    /// stock) whose base unit has no kg conversion — a liquid in ml, tablets in pieces. Their
    /// allocation rows hold that unit in AllocatedKg, so kg totals must leave them out (their cost
    /// still counts).
    /// </summary>
    private async Task<List<Guid>> NonKgFormulaIdsAsync(CancellationToken cancellationToken)
    {
        var candidates = await (
                from f in _context.FeedFormulas.AsNoTracking()
                where f.Ingredients.Count == 1
                from fi in f.Ingredients
                join ing in _context.FeedIngredients on fi.IngredientId equals ing.Id
                where ing.InventoryItemId != null
                join item in _context.InventoryItems on ing.InventoryItemId equals item.Id
                select new { FormulaId = f.Id, item.UnitOfMeasure, item.BaseUnitId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return [];

        var units = await _context.MeasureUnits.AsNoTracking()
            .Include(u => u.Conversions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Where(c =>
            {
                var unit = (c.BaseUnitId is { } id ? units.FirstOrDefault(u => u.Id == id) : null)
                           ?? units.FirstOrDefault(u => UnitService.SameUnit(u.Name, c.UnitOfMeasure));
                return !FeedsAsKg(c.UnitOfMeasure, unit);
            })
            .Select(c => c.FormulaId)
            .Distinct()
            .ToList();
    }

    private static bool FeedsAsKg(string unitOfMeasure, MeasureUnit? unit) =>
        Farm360.Application.Feeding.Services.FeedUnits.FeedsAsKg(unitOfMeasure, unit?.Conversions.Select(c => c.Name));

    public async Task<AnimalFeedTotals> GetTotalsForAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var nonKg = await NonKgFormulaIdsAsync(cancellationToken).ConfigureAwait(false);

        // Aggregated in the database — a year of daily rows should never be pulled into memory
        // just to add up two columns.
        var result = await Eaten()
            .Where(a => a.AnimalId == animalId && a.EntryDate >= from && a.EntryDate <= to)
            .GroupBy(a => 1)
            .Select(g => new
            {
                TotalKg = g.Sum(x => nonKg.Contains(x.FormulaId) ? 0m : x.AllocatedKg),
                TotalCost = g.Sum(x => x.AllocatedCostBdt),
                Days = g.Select(x => x.EntryDate).Distinct().Count(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? new AnimalFeedTotals(animalId, 0m, 0m, 0)
            : new AnimalFeedTotals(animalId, result.TotalKg, result.TotalCost, result.Days);
    }

    public async Task<IReadOnlyList<AnimalFeedTotals>> GetTotalsForFarmAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var nonKg = await NonKgFormulaIdsAsync(cancellationToken).ConfigureAwait(false);

        var rows = await Eaten()
            .Where(a => a.FarmId == farmId && a.EntryDate >= from && a.EntryDate <= to)
            .GroupBy(a => a.AnimalId)
            .Select(g => new
            {
                AnimalId = g.Key,
                TotalKg = g.Sum(x => nonKg.Contains(x.FormulaId) ? 0m : x.AllocatedKg),
                TotalCost = g.Sum(x => x.AllocatedCostBdt),
                Days = g.Select(x => x.EntryDate).Distinct().Count(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new AnimalFeedTotals(r.AnimalId, r.TotalKg, r.TotalCost, r.Days))
            .ToList();
    }

    public async Task<IReadOnlyList<AnimalFeedAllocation>> GetNonZeroForSkippedEntriesAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        await (
                from a in _context.AnimalFeedAllocations
                join e in _context.DailyFeedingEntries on a.DailyFeedingEntryId equals e.Id
                where a.FarmId == farmId
                      && e.Status == DailyFeedingEntryStatus.Skipped
                      && (a.AllocatedKg != 0 || a.AllocatedCostBdt != 0)
                select a)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> GetAnimalIdsByFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        await _context.AnimalFeedAllocations
            .AsNoTracking()
            .Where(a => a.FarmId == farmId)
            .Select(a => a.AnimalId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<HashSet<Guid>> GetAllocatedEntryIdsAcrossTenantsByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var ids = await _context.AnimalFeedAllocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.EntryDate == date)
            .Select(a => a.DailyFeedingEntryId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. ids];
    }

    public async Task<IReadOnlyList<DailyFeedingEntry>> GetUnallocatedEntriesAcrossTenantsAsync(
        DateOnly from,
        DateOnly to,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Anti-join against the allocation table so a resumed or re-run backfill picks up only
        // what is still missing — the operation has to be safely repeatable.
        var allocatedIds = _context.AnimalFeedAllocations
            .IgnoreQueryFilters()
            .Select(a => a.DailyFeedingEntryId);

        return await _context.DailyFeedingEntries
            .IgnoreQueryFilters()
            .Where(e => e.EntryDate >= from && e.EntryDate <= to && !allocatedIds.Contains(e.Id))
            .OrderBy(e => e.EntryDate)
            .Take(Math.Clamp(take, 1, 10_000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AnimalFeedAllocation>> GetByEntryIdAcrossTenantsAsync(
        Guid entryId,
        CancellationToken cancellationToken = default) =>
        await _context.AnimalFeedAllocations
            .IgnoreQueryFilters()
            .Where(a => a.DailyFeedingEntryId == entryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void UpdateRange(IEnumerable<AnimalFeedAllocation> allocations) =>
        _context.AnimalFeedAllocations.UpdateRange(allocations);
}
