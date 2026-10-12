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

    /// <summary>Removes allocation rows (soft-deleted by the audit interceptor, so history is kept).</summary>
    void RemoveRange(IEnumerable<AnimalOverheadAllocation> allocations);

    /// <summary>
    /// Which of these source transactions already have live allocation rows, so a re-run skips
    /// them. Matched on the transaction itself, never on a date range: a transaction whose date
    /// moved to another month must still be recognised as allocated.
    /// The first of two defences against double-posting; the unique index is the second.
    /// </summary>
    Task<HashSet<Guid>> GetAllocatedTransactionIdsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken = default);

    /// <summary>Every live allocation row produced by one source transaction, tracked for removal.</summary>
    Task<IReadOnlyList<AnimalOverheadAllocation>> GetBySourceTransactionAsync(
        Guid transactionId,
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
