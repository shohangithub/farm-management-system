using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Feeding;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Reporting.Definitions.Inventory;

public sealed record DailyInventoryConsumptionReportRow(
    DateOnly Date,
    string ItemName,
    string Category,
    string Unit,
    decimal ConsumedQuantity,
    decimal UnitCostBdt,
    decimal TotalCostBdt);

/// <summary>
/// Report IN2 — Daily stock usage: day by day, every item taken out of stock (feed, medicine,
/// vaccines, supplies), how much and what it cost.
/// </summary>
public sealed class DailyInventoryConsumptionReportDefinition : ReportDefinition<DailyInventoryConsumptionReportRow>
{
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IInventoryItemRepository _itemRepository;

    public DailyInventoryConsumptionReportDefinition(
        IStockTransactionRepository transactionRepository,
        IInventoryItemRepository itemRepository)
    {
        _transactionRepository = transactionRepository;
        _itemRepository = itemRepository;
    }

    public override string Key => "inventory.daily-consumption";

    public override LocalizedText Title => new("Daily Stock Usage Report", "দৈনিক মজুদ ব্যবহার প্রতিবেদন");

    public override LocalizedText Description => new(
        "What was taken out of stock each day — feed, medicine, vaccines and supplies — how much, and what it cost.",
        "প্রতিদিন মজুদ থেকে কী বের হয়েছে — খাদ্য, ওষুধ, টিকা ও সামগ্রী — কত পরিমাণ এবং কত খরচ।");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
    ];

    // The date is the group band, so it is not repeated as a column.
    public override IReadOnlyList<ReportColumn<DailyInventoryConsumptionReportRow>> Columns =>
    [
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("item", new LocalizedText("Item", "উপকরণ"), r => r.ItemName, width: 3.0f),
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("category", new LocalizedText("Category", "শ্রেণী"), r => r.Category, width: 1.6f),
        ReportColumn<DailyInventoryConsumptionReportRow>.Number("quantity", new LocalizedText("Quantity used", "ব্যবহৃত পরিমাণ"), r => r.ConsumedQuantity, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum, unitOf: r => r.Unit),
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 16f, relative: false),
        ReportColumn<DailyInventoryConsumptionReportRow>.Money("rate", new LocalizedText("Average rate (BDT per unit)", "গড় দর (টাকা/একক)"), r => r.UnitCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.None),
        ReportColumn<DailyInventoryConsumptionReportRow>.Money("totalCost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.TotalCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<DailyInventoryConsumptionReportRow>? Group =>
        ReportGroup<DailyInventoryConsumptionReportRow>.ByLocalized(
            r => r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r => new LocalizedText($"Date: {FeedReportText.Day(r.Date)}", $"তারিখ: {FeedReportText.Day(r.Date)}"));

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "পুরো সময়ের মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one item taken out of stock on one day: fed to animals, used for treatment, used as a supply, or issued by hand.",
            "প্রতিটি সারি একদিনে মজুদ থেকে বের হওয়া একটি উপকরণ: পশুকে খাওয়ানো, চিকিৎসায় ব্যবহার, সামগ্রী হিসেবে ব্যবহার, বা হাতে দেওয়া।"),
        new("Average rate = cost ÷ quantity used that day, at the stock's average purchase price.",
            "গড় দর = খরচ ÷ সেদিনের ব্যবহৃত পরিমাণ, মজুদের গড় ক্রয়মূল্যে।"),
        new("Quantities are totalled only when every row shares the same unit; cost is always totalled.",
            "সব সারির একক এক হলে তবেই পরিমাণ যোগ করা হয়; খরচ সবসময় যোগ করা হয়।"),
        new("Feed put back into stock (for example feed booked for an animal that had already been sold) is not counted as used.",
            "মজুদে ফেরত আসা খাদ্য (যেমন আগেই বিক্রি হওয়া পশুর জন্য লেখা খাদ্য) ব্যবহার হিসেবে ধরা হয় না।"),
        new("Stock written off, adjusted after a count, or returned to the supplier is not included.",
            "নষ্ট হিসেবে বাদ দেওয়া, গণনার পর সমন্বয় করা বা সরবরাহকারীকে ফেরত দেওয়া মজুদ এতে নেই।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = FeedReportText.Period(context.Range("period"));
        var what = new LocalizedText("everything taken out of stock", "মজুদ থেকে বের হওয়া সবকিছু").For(context.Language);
        return Task.FromResult<string?>($"{period} · {what}");
    }

    protected override async Task<IReadOnlyList<DailyInventoryConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var period = context.Range("period");
        var language = context.Language;

        var consumptionTx = await StockUsageTransactions
            .LoadAsync(_transactionRepository, farmId, period, itemId: null, cancellationToken)
            .ConfigureAwait(false);

        if (consumptionTx.Count == 0)
        {
            return [];
        }

        var items = await _itemRepository
            .GetByFarmIdAsync(farmId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var itemsById = items.ToDictionary(i => i.Id);
        var deletedItem = new LocalizedText("Deleted item", "মুছে ফেলা উপকরণ").For(language);

        var rows = consumptionTx
            .GroupBy(tx => (Date: tx.TransactionDate, ItemId: tx.InventoryItemId))
            .Select(g =>
            {
                itemsById.TryGetValue(g.Key.ItemId, out var item);
                var itemName = item?.Name ?? deletedItem;
                var category = FeedReportText.ItemCategory(item?.Category ?? InventoryCategory.Other).For(language);
                var unit = item?.UnitOfMeasure ?? "unit";

                var totalQuantity = g.Sum(tx => tx.Quantity);
                var totalCost = g.Sum(tx => tx.TotalCostBdt);
                var avgRate = totalQuantity > 0 ? Math.Round(totalCost / totalQuantity, 2) : 0m;

                return new DailyInventoryConsumptionReportRow(
                    Date: g.Key.Date,
                    ItemName: itemName,
                    Category: category,
                    Unit: unit,
                    ConsumedQuantity: totalQuantity,
                    UnitCostBdt: avgRate,
                    TotalCostBdt: totalCost);
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Category, StringComparer.CurrentCulture)
            .ThenBy(r => r.ItemName, StringComparer.CurrentCulture)
            .ToList();

        return rows;
    }
}

/// <summary>
/// The stock movements that count as "used" for the usage reports: feed, medicine and supplies
/// consumed, and manual issues — minus feed that was put back into stock because its feeding was
/// reversed (the animal had already left the farm).
/// </summary>
internal static class StockUsageTransactions
{
    private static readonly HashSet<StockTransactionType> UsageTypes =
    [
        StockTransactionType.AutoFeedConsumption,
        StockTransactionType.PlannedFeedConsumption,
        StockTransactionType.AutoMedicineConsumption,
        StockTransactionType.AutoConsumableUsage,
        StockTransactionType.ManualStockOut,
    ];

    public static async Task<List<StockTransaction>> LoadAsync(
        IStockTransactionRepository transactions,
        Guid farmId,
        DateRange period,
        Guid? itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        // No upper bound: a reversal is dated the day it was made, which can be after the period
        // that holds the feed it cancels.
        var movements = await transactions
            .GetByFarmIdAsync(farmId, period.From, null, cancellationToken)
            .ConfigureAwait(false);

        var returned = movements
            .Where(t => t.TransactionType == StockTransactionType.FeedConsumptionReversal && t.ReferenceId.HasValue)
            .Select(t => (Entry: t.ReferenceId!.Value, Item: t.InventoryItemId))
            .ToHashSet();

        return movements
            .Where(t => (itemId is null || t.InventoryItemId == itemId.Value)
                && period.Contains(t.TransactionDate)
                && UsageTypes.Contains(t.TransactionType)
                && !(t.TransactionType == StockTransactionType.PlannedFeedConsumption
                     && t.ReferenceId is { } entry
                     && returned.Contains((entry, t.InventoryItemId))))
            .ToList();
    }
}
