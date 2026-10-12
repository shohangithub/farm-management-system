using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// Turns stock movements into ledger postings so the Inventory asset in the books always moves
/// with the physical stock:
/// <list type="bullet">
/// <item>Stock in (paid) — Dr Inventory, Cr Cash. Not an expense yet.</item>
/// <item>Stock in (owner / opening) — Dr Inventory, Cr Owner's Capital.</item>
/// <item>Stock consumed — Dr Feed / Medicine / Consumables expense, Cr Inventory, at weighted
/// average cost. This is when the cost reaches profit &amp; loss.</item>
/// <item>Stock written off — Dr Write-off expense, Cr Inventory.</item>
/// </list>
/// Every posting is keyed on the stock transaction (or source document) id, so it is idempotent.
/// </summary>
public static class InventoryAccounting
{
    public const string Module = "Inventory";

    /// <summary>The expense head stock of this category is charged to when it is used.</summary>
    public static TransactionCategory ConsumptionCategory(InventoryCategory category) => category switch
    {
        InventoryCategory.Feed => TransactionCategory.FeedCost,
        InventoryCategory.Medicine or InventoryCategory.Vaccine => TransactionCategory.MedicineCost,
        _ => TransactionCategory.ConsumableExpense,
    };

    public static Task<FinancialTransaction?> PostStockInAsync(
        this ILedgerPostingService posting,
        InventoryItem item,
        decimal amountBdt,
        DateOnly date,
        Guid sourceDocumentId,
        StockFundingSource funding,
        string reference,
        string description,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(item);

        var owner = funding == StockFundingSource.OwnerContribution;
        return posting.PostAsync(new LedgerPosting(
            item.TenantId,
            item.FarmId,
            owner ? TransactionCategory.OwnerCapital : TransactionCategory.InventoryPurchase,
            amountBdt,
            date.ToDateTime(TimeOnly.MinValue),
            sourceDocumentId,
            Module,
            description,
            Settlement: owner ? SettlementAccount.Inventory : SettlementAccount.Cash,
            Notes: owner ? $"{item.Name} brought into stock without a farm payment" : $"{item.Name} added to stock",
            ReferenceId: reference), cancellationToken);
    }

    public static Task PostConsumptionAsync(
        this ILedgerPostingService posting,
        InventoryItem item,
        decimal quantity,
        decimal unitCostBdt,
        DateOnly date,
        Guid sourceDocumentId,
        string description,
        TransactionCategory? category = null,
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(item);

        return posting.PostAsync(new LedgerPosting(
            item.TenantId,
            item.FarmId,
            category ?? ConsumptionCategory(item.Category),
            Math.Round(quantity * unitCostBdt, 2),
            date.ToDateTime(TimeOnly.MinValue),
            sourceDocumentId,
            Module,
            description,
            Settlement: SettlementAccount.Inventory,
            Notes: $"{quantity:0.###} {item.UnitOfMeasure} of {item.Name} @ {unitCostBdt:0.##} BDT (from stock)",
            AnimalId: animalId,
            BatchId: batchId,
            ShedId: shedId), cancellationToken);
    }
}
