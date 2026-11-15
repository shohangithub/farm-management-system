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

    /// <summary>Allocations for a farm within a date range with optional filters for animal, batch, and shed.</summary>
    Task<IReadOnlyList<AnimalFeedAllocation>> GetByFarmAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Feed actually eaten by one animal: only rows whose entry is Confirmed or Adjusted.
    /// TotalKg leaves out non-kg quantities (a liquid fed in ml); TotalCostBdt includes them.
    /// </summary>
    Task<AnimalFeedTotals> GetTotalsForAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Per-animal totals across a farm (same rules as <see cref="GetTotalsForAnimalAsync"/>), for herd-level ranking and reconciliation.</summary>
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

    /// <summary>Tracked rows of a farm's skipped entries that still carry feed or cost (for the resync to zero).</summary>
    Task<IReadOnlyList<AnimalFeedAllocation>> GetNonZeroForSkippedEntriesAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);

    /// <summary>Every animal that has any allocation row on the farm.</summary>
    Task<IReadOnlyList<Guid>> GetAnimalIdsByFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);
}
