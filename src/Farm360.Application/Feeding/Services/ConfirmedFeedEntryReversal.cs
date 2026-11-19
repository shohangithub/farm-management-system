using System.Globalization;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Feeding.Services;

/// <summary>
/// Undoes a confirmed feeding entry. The stock goes back at the cost it was issued at, the
/// feed-cost ledger postings are removed, the entry becomes Skipped with the reason, and its
/// per-animal rows are zeroed. Used when an animal's exit is recorded late, and when a daily feed
/// sheet line is corrected or removed. The caller saves, then re-syncs the touched animals'
/// feed ledger buckets.
/// </summary>
public static class ConfirmedFeedEntryReversal
{
    public static async Task ReverseAsync(
        DailyFeedingEntry entry,
        string reason,
        HashSet<Guid> touched,
        IStockTransactionRepository stockTransactions,
        IInventoryItemRepository inventory,
        ILedgerPostingService ledgerPosting,
        IDailyFeedingEntryRepository entries,
        IAnimalFeedAllocationRepository allocations,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(touched);
        ArgumentNullException.ThrowIfNull(stockTransactions);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(ledgerPosting);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(allocations);

        var movements = await stockTransactions.GetByFarmIdAsync(entry.FarmId, entry.EntryDate, null, cancellationToken).ConfigureAwait(false);
        var mine = movements.Where(t => t.ReferenceId == entry.Id).ToList();
        var alreadyReturned = mine
            .Where(t => t.TransactionType == StockTransactionType.FeedConsumptionReversal)
            .Select(t => t.InventoryItemId)
            .ToHashSet();

        foreach (var issued in mine.Where(t => t.TransactionType == StockTransactionType.PlannedFeedConsumption))
        {
            if (!alreadyReturned.Add(issued.InventoryItemId))
                continue;

            var item = await inventory.GetByIdAsync(issued.InventoryItemId, cancellationToken).ConfigureAwait(false);
            if (item is null)
                continue;

            item.ReturnConsumedStock(issued.Quantity, issued.UnitCostBdt);
            inventory.Update(item);

            await stockTransactions.AddAsync(new StockTransaction(
                id: Guid.NewGuid(),
                tenantId: entry.TenantId,
                farmId: entry.FarmId,
                inventoryItemId: item.Id,
                transactionType: StockTransactionType.FeedConsumptionReversal,
                quantity: issued.Quantity,
                unitCostBdt: issued.UnitCostBdt,
                balanceAfter: item.CurrentStock,
                transactionDate: today < entry.EntryDate ? entry.EntryDate : today,
                reason: $"{reason}: feed for {entry.EntryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} returned to stock (entry {entry.Id})",
                referenceId: entry.Id), cancellationToken).ConfigureAwait(false);
        }

        // The feed handler books two postings per entry: feed from stock (keyed on the entry) and
        // ingredients not tracked in stock (keyed on its "direct-feed" sub key). Both go.
        await ledgerPosting.ReverseAsync(entry.FarmId, entry.Id, TransactionCategory.FeedCost, cancellationToken).ConfigureAwait(false);
        await ledgerPosting.ReverseAsync(entry.FarmId, LedgerPosting.SubKey(entry.Id, "direct-feed"), TransactionCategory.FeedCost, cancellationToken).ConfigureAwait(false);

        entry.VoidAfterExit(reason);
        entries.Update(entry);

        var rows = await allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 0)
        {
            foreach (var row in rows)
                row.UpdateConsumption(0m, 0m, row.UnitCostBdtPerKg);
            allocations.UpdateRange(rows);
            touched.UnionWith(rows.Select(r => r.AnimalId));
        }
    }
}
