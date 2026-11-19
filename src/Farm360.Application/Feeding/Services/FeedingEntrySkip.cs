using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;

namespace Farm360.Application.Feeding.Services;

/// <summary>
/// Skipping a pending entry: the one path the Skip command and exit processing share. A pending
/// entry is a plan, not a fact, so it can be dropped freely: it is marked Skipped and the
/// per-animal rows written when it was generated (carrying the planned feed and cost) are zeroed.
/// The caller saves, then re-syncs the returned animals' ledger feed buckets.
/// </summary>
public static class FeedingEntrySkip
{
    public static async Task<IReadOnlyList<AnimalFeedAllocation>> SkipAsync(
        DailyFeedingEntry entry,
        string reason,
        IDailyFeedingEntryRepository entries,
        IAnimalFeedAllocationRepository? allocations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);

        entry.Skip(reason);
        entries.Update(entry);

        if (allocations is null)
            return [];

        var rows = await allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 0)
        {
            foreach (var row in rows)
                row.UpdateConsumption(0m, 0m, row.UnitCostBdtPerKg);
            allocations.UpdateRange(rows);
        }

        return rows;
    }
}
