using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Livestock;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <summary>
/// Reads a farm's per-animal growing costs for <see cref="ILivestockCarriedCostService"/> in four
/// grouped queries (animals, feed eaten, labour/overhead allocations, other tagged expense postings).
/// </summary>
public sealed class LivestockCarriedCostSource : ILivestockCarriedCostSource
{
    private readonly ApplicationDbContext _context;

    public LivestockCarriedCostSource(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LivestockCarriedCostData> LoadAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default)
    {
        var animalRows = await _context.Animals.AsNoTracking()
            .Where(a => a.FarmId == farmId && a.AcquisitionDate <= upTo)
            .Select(a => new { a.Id, a.AcquisitionDate, a.SaleDate, a.DeathDate, a.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var animals = animalRows
            .Select(a => new CarriedCostAnimal(a.Id, a.AcquisitionDate, a.SaleDate ?? a.DeathDate, AnimalStatusGroups.HasLeftFarm(a.Status)))
            .ToList();

        var costs = new List<AnimalGrowingCost>();

        // ── Feed actually eaten: confirmed / adjusted feeding entries only ──
        var feed = await (
                from a in _context.AnimalFeedAllocations.AsNoTracking()
                join e in _context.DailyFeedingEntries.AsNoTracking() on a.DailyFeedingEntryId equals e.Id
                where a.FarmId == farmId
                      && a.EntryDate <= upTo
                      && (e.Status == DailyFeedingEntryStatus.Confirmed || e.Status == DailyFeedingEntryStatus.Adjusted)
                group a by new { a.AnimalId, a.EntryDate } into g
                select new { g.Key.AnimalId, g.Key.EntryDate, Cost = g.Sum(x => x.AllocatedCostBdt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        costs.AddRange(feed.Select(f => new AnimalGrowingCost(f.AnimalId, f.EntryDate, f.Cost)));

        // ── Labour and overhead allocations, dated by the expense they came from ──
        // "Everything so far" (DateOnly.MaxValue, e.g. money-account balances) has no next day.
        var upToExclusive = upTo < DateOnly.MaxValue ? upTo.AddDays(1).ToDateTime(TimeOnly.MinValue) : DateTime.MaxValue;
        var overhead = await (
                from o in _context.AnimalOverheadAllocations.AsNoTracking()
                join t in _context.FinancialTransactions.AsNoTracking() on o.SourceTransactionId equals t.Id
                where o.FarmId == farmId && t.TransactionDate < upToExclusive
                group new { o, t } by new { o.AnimalId, t.TransactionDate, t.Type } into g
                select new { g.Key.AnimalId, g.Key.TransactionDate, g.Key.Type, Amount = g.Sum(x => x.o.AllocatedAmountBdt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        // A refund (income-type row) shared out reduces the cost.
        costs.AddRange(overhead.Select(o => new AnimalGrowingCost(
            o.AnimalId,
            DateOnly.FromDateTime(o.TransactionDate),
            o.Type == TransactionType.Income ? -o.Amount : o.Amount)));

        // ── Other expense postings tagged to an animal (treatments, medicine & vaccine stock, vet fees) ──
        var categories = LivestockCarriedCostService.TaggedPostingCategories;
        const string feeding = LivestockCarriedCostService.FeedingSourceModule;
        var tagged = await _context.FinancialTransactions.AsNoTracking()
            .Where(t => t.FarmId == farmId
                        && t.AnimalId != null
                        && t.TransactionDate < upToExclusive
                        && categories.Contains(t.Category)
                        // The farm-level feed posting: its per-animal cost is the feed allocation above.
                        && !(t.Category == TransactionCategory.FeedCost && t.IsAutomated && t.SourceModule == feeding)
                        // Already counted through its allocation row.
                        && !_context.AnimalOverheadAllocations.Any(o => o.SourceTransactionId == t.Id))
            .GroupBy(t => new { AnimalId = t.AnimalId!.Value, t.TransactionDate, t.Type })
            .Select(g => new { g.Key.AnimalId, g.Key.TransactionDate, g.Key.Type, Amount = g.Sum(x => x.AmountBdt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        costs.AddRange(tagged.Select(t => new AnimalGrowingCost(
            t.AnimalId,
            DateOnly.FromDateTime(t.TransactionDate),
            t.Type == TransactionType.Income ? -t.Amount : t.Amount)));

        return new LivestockCarriedCostData(animals, costs);
    }
}
