using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

/// <summary>One animal's allocated indirect cost, split by ledger bucket.</summary>
public readonly record struct AnimalOverheadTotals(Guid AnimalId, decimal LabourBdt, decimal OverheadBdt);

public interface IAnimalOverheadAllocationRepository
{
    void AddRange(IEnumerable<AnimalOverheadAllocation> allocations);

    /// <summary>
    /// Source transaction ids already allocated, so a re-run skips them.
    /// The first of two defences against double-posting; the unique index is the second.
    /// </summary>
    Task<HashSet<Guid>> GetAllocatedTransactionIdsAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifetime labour and overhead totals for one animal. The ledger buckets are recomputed from
    /// this, which is what makes the posting idempotent.
    /// </summary>
    Task<AnimalOverheadTotals> GetTotalsForAnimalAsync(
        Guid animalId,
        CancellationToken cancellationToken = default);

    /// <summary>Lifetime totals for every animal touched by a run, in one round trip.</summary>
    Task<IReadOnlyList<AnimalOverheadTotals>> GetTotalsForAnimalsAsync(
        IEnumerable<Guid> animalIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnimalOverheadAllocation>> GetByAnimalAsync(
        Guid animalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
