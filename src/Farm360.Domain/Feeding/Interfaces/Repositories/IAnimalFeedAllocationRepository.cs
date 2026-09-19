using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Feeding.Interfaces.Repositories;

/// <summary>One animal's feed totals over a period, for cost ledgers and summary reports.</summary>
public readonly record struct AnimalFeedTotals(Guid AnimalId, decimal TotalKg, decimal TotalCostBdt, int Days);

public interface IAnimalFeedAllocationRepository
{
    void Add(AnimalFeedAllocation allocation);

    void AddRange(IEnumerable<AnimalFeedAllocation> allocations);

    /// <summary>Every allocation for one animal in a date range, oldest first. Powers report A1.</summary>
    Task<IReadOnlyList<AnimalFeedAllocation>> GetByAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<AnimalFeedTotals> GetTotalsForAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Per-animal totals across a farm, for herd-level ranking and reconciliation.</summary>
    Task<IReadOnlyList<AnimalFeedTotals>> GetTotalsForFarmAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Entry ids already allocated on a date, so a re-run of the daily job cannot double-post.
    /// Crosses tenants because the job runs for the whole platform.
    /// </summary>
    Task<HashSet<Guid>> GetAllocatedEntryIdsAcrossTenantsByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Feeding entries in a date range that have no allocation rows yet, oldest first.
    /// Drives the historical backfill; crosses tenants because the backfill is an admin operation.
    /// </summary>
    Task<IReadOnlyList<DailyFeedingEntry>> GetUnallocatedEntriesAcrossTenantsAsync(
        DateOnly from,
        DateOnly to,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnimalFeedAllocation>> GetByEntryIdAcrossTenantsAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    void UpdateRange(IEnumerable<AnimalFeedAllocation> allocations);
}
