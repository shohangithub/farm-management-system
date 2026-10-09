using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
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
/// Report IN2 — Comprehensive Daily Inventory & Feed Consumption Statement.
/// Shows date-wise and item-wise breakdown of feed, medicine, vaccines, and consumables used across farm operations.
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

    public override LocalizedText Title => new("Daily Inventory Consumption Report", "দৈনিক উপাদানভিত্তিক মজুদ সামগ্রী ব্যবহার প্রতিবেদন");

    public override LocalizedText Description => new(
        "Date-wise and item-wise daily consumption of feed, medicine, vaccines, and operational supplies.",
        "তারিখভিত্তিক ও উপাদানভিত্তিক পশুখাদ্য, ওষুধ ও সামগ্রীর দৈনিক ব্যবহার ও আর্থিক খরচের প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
    ];

    public override IReadOnlyList<ReportColumn<DailyInventoryConsumptionReportRow>> Columns =>
    [
        ReportColumn<DailyInventoryConsumptionReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f, mergeRepeating: true),
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("item", new LocalizedText("Item Name", "পণ্য / উপাদান"), r => r.ItemName, width: 3.0f),
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("category", new LocalizedText("Category", "শ্রেণী"), r => r.Category, width: 1.8f),
        ReportColumn<DailyInventoryConsumptionReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<DailyInventoryConsumptionReportRow>.Number("quantity", new LocalizedText("Consumed Qty", "ব্যবহৃত পরিমাণ"), r => r.ConsumedQuantity, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<DailyInventoryConsumptionReportRow>.Money("rate", new LocalizedText("Avg Rate (BDT)", "গড় দর (টাকা)"), r => r.UnitCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.None),
        ReportColumn<DailyInventoryConsumptionReportRow>.Money("totalCost", new LocalizedText("Total Cost (BDT)", "মোট খরচ (টাকা)"), r => r.TotalCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = context.Range("period");
        return $"Farm Daily Inventory Consumption Statement ({period.From:dd-MMM-yyyy} to {period.To:dd-MMM-yyyy})";
    }

    protected override async Task<IReadOnlyList<DailyInventoryConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.Id("farmId");
        var period = context.Range("period");

        var transactions = await _transactionRepository
            .GetByFarmIdAsync(farmId, period.From, period.To, cancellationToken)
            .ConfigureAwait(false);

        var consumptionTx = transactions
            .Where(t =>
                t.TransactionType == StockTransactionType.AutoFeedConsumption ||
                t.TransactionType == StockTransactionType.PlannedFeedConsumption ||
                t.TransactionType == StockTransactionType.AutoMedicineConsumption ||
                t.TransactionType == StockTransactionType.AutoConsumableUsage ||
                t.TransactionType == StockTransactionType.ManualStockOut)
            .ToList();

        if (consumptionTx.Count == 0)
        {
            return [];
        }

        var items = await _itemRepository
            .GetByFarmIdAsync(farmId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var itemsById = items.ToDictionary(i => i.Id);

        var rows = consumptionTx
            .GroupBy(tx => (Date: tx.TransactionDate, ItemId: tx.InventoryItemId))
            .Select(g =>
            {
                itemsById.TryGetValue(g.Key.ItemId, out var item);
                var itemName = item?.Name ?? "Item #" + g.Key.ItemId.ToString()[..8];
                var category = item?.Category.ToString() ?? "General";
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
            .OrderByDescending(r => r.Date)
            .ThenBy(r => r.Category)
            .ThenBy(r => r.ItemName)
            .ToList();

        return rows;
    }
}
