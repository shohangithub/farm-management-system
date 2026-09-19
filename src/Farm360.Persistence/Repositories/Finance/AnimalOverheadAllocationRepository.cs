using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Finance;

public sealed class AnimalOverheadAllocationRepository : IAnimalOverheadAllocationRepository
{
    private readonly ApplicationDbContext _context;

    public AnimalOverheadAllocationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void AddRange(IEnumerable<AnimalOverheadAllocation> allocations) =>
        _context.AnimalOverheadAllocations.AddRange(allocations);

    public async Task<HashSet<Guid>> GetAllocatedTransactionIdsAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Matched on the source transaction, not the period: a monthly wage bill posted in
        // January but covering December must not be re-allocated when December is re-run.
        var ids = await _context.AnimalOverheadAllocations
            .AsNoTracking()
            .Where(a => a.FarmId == farmId && a.PeriodEnd >= from && a.PeriodStart <= to)
            .Select(a => a.SourceTransactionId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. ids];
    }

    public async Task<AnimalOverheadTotals> GetTotalsForAnimalAsync(
        Guid animalId,
        CancellationToken cancellationToken = default)
    {
        var result = await _context.AnimalOverheadAllocations
            .AsNoTracking()
            .Where(a => a.AnimalId == animalId)
            .GroupBy(a => a.AnimalId)
            .Select(g => new
            {
                Labour = g.Where(x => x.Bucket == OverheadBucket.Labour).Sum(x => (decimal?)x.AllocatedAmountBdt) ?? 0m,
                Overhead = g.Where(x => x.Bucket == OverheadBucket.Overhead).Sum(x => (decimal?)x.AllocatedAmountBdt) ?? 0m,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? new AnimalOverheadTotals(animalId, 0m, 0m)
            : new AnimalOverheadTotals(animalId, result.Labour, result.Overhead);
    }

    public async Task<IReadOnlyList<AnimalOverheadTotals>> GetTotalsForAnimalsAsync(
        IEnumerable<Guid> animalIds,
        CancellationToken cancellationToken = default)
    {
        var ids = animalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // Grouped in the database: a monthly run can touch thousands of animals, and a query
        // per animal would turn one round trip into thousands.
        var rows = await _context.AnimalOverheadAllocations
            .AsNoTracking()
            .Where(a => ids.Contains(a.AnimalId))
            .GroupBy(a => a.AnimalId)
            .Select(g => new
            {
                AnimalId = g.Key,
                Labour = g.Where(x => x.Bucket == OverheadBucket.Labour).Sum(x => (decimal?)x.AllocatedAmountBdt) ?? 0m,
                Overhead = g.Where(x => x.Bucket == OverheadBucket.Overhead).Sum(x => (decimal?)x.AllocatedAmountBdt) ?? 0m,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new AnimalOverheadTotals(r.AnimalId, r.Labour, r.Overhead))
            .ToList();
    }

    public async Task<IReadOnlyList<AnimalOverheadAllocation>> GetByAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _context.AnimalOverheadAllocations
            .AsNoTracking()
            .Where(a => a.AnimalId == animalId && a.PeriodEnd >= from && a.PeriodStart <= to)
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
