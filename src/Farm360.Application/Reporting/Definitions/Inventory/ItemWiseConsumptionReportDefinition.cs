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

public sealed record ItemWiseConsumptionReportRow(
    DateOnly Date,
    decimal ConsumedQuantity,
    string Unit,
    decimal UnitCostBdt,
    decimal TotalCostBdt);

/// <summary>
/// Report IN3 — Item-wise Inventory Daily Consumption Statement.
/// Shows consumption for a selected inventory item, with date-by-date daily breakdowns.
/// </summary>
public sealed class ItemWiseConsumptionReportDefinition : ReportDefinition<ItemWiseConsumptionReportRow>
{
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IInventoryItemRepository _itemRepository;

    public ItemWiseConsumptionReportDefinition(
        IStockTransactionRepository transactionRepository,
        IInventoryItemRepository itemRepository)
    {
        _transactionRepository = transactionRepository;
        _itemRepository = itemRepository;
    }

    public override string Key => "inventory.item-consumption";

    public override LocalizedText Title => new("Item Daily Consumption Statement", "উপাদানভিত্তিক দৈনিক ব্যবহার বিবরণী");

    public override LocalizedText Description => new(
        "Date-by-date daily consumption statement for a selected inventory item over the chosen period.",
        "নির্দিষ্ট সময়কালে নির্বাচিত মজুদ উপাদানের তারিখওয়ারী দৈনিক ব্যবহারের প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.InventoryItem(name: "inventoryItemId", required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
    ];

    public override IReadOnlyList<ReportColumn<ItemWiseConsumptionReportRow>> Columns =>
    [
        ReportColumn<ItemWiseConsumptionReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 30f),
        ReportColumn<ItemWiseConsumptionReportRow>.Number("quantity", new LocalizedText("Consumed Qty", "ব্যবহৃত পরিমাণ"), r => r.ConsumedQuantity, decimals: 2, widthMm: 35f, aggregate: ReportAggregate.Sum),
        ReportColumn<ItemWiseConsumptionReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 22f, relative: false),
        ReportColumn<ItemWiseConsumptionReportRow>.Money("rate", new LocalizedText("Avg Rate (BDT)", "গড় দর (টাকা)"), r => r.UnitCostBdt, decimals: 2, widthMm: 35f, aggregate: ReportAggregate.None),
        ReportColumn<ItemWiseConsumptionReportRow>.Money("totalCost", new LocalizedText("Total Cost (BDT)", "মোট খরচ (টাকা)"), r => r.TotalCostBdt, decimals: 2, widthMm: 40f, aggregate: ReportAggregate.Sum),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = context.Range("period");
        var selectedItemId = context.Id("inventoryItemId");

        var item = await _itemRepository.GetByIdAsync(selectedItemId, cancellationToken).ConfigureAwait(false);
        if (item is not null)
        {
            var isBn = context.Language == ReportLanguage.Bn;
            return isBn
                ? $"মজুদ উপাদান: {item.Name}  |  ধরণ / শ্রেণী: {item.Category}  |  একক: {item.UnitOfMeasure}  |  সময়কাল: {period.From:dd-MMM-yyyy} হতে {period.To:dd-MMM-yyyy}"
                : $"Item: {item.Name}  |  Type / Category: {item.Category}  |  Unit: {item.UnitOfMeasure}  |  Period: {period.From:dd-MMM-yyyy} to {period.To:dd-MMM-yyyy}";
        }

        return $"Item Daily Consumption Statement ({period.From:dd-MMM-yyyy} to {period.To:dd-MMM-yyyy})";
    }

    protected override async Task<IReadOnlyList<ItemWiseConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.Id("farmId");
        var period = context.Range("period");
        var selectedItemId = context.Id("inventoryItemId");

        var item = await _itemRepository.GetByIdAsync(selectedItemId, cancellationToken).ConfigureAwait(false);
        var unit = item?.UnitOfMeasure ?? "unit";

        var transactions = await _transactionRepository
            .GetByFarmIdAsync(farmId, period.From, period.To, cancellationToken)
            .ConfigureAwait(false);

        var consumptionTx = transactions
            .Where(t => t.InventoryItemId == selectedItemId &&
                (t.TransactionType == StockTransactionType.AutoFeedConsumption ||
                 t.TransactionType == StockTransactionType.PlannedFeedConsumption ||
                 t.TransactionType == StockTransactionType.AutoMedicineConsumption ||
                 t.TransactionType == StockTransactionType.AutoConsumableUsage ||
                 t.TransactionType == StockTransactionType.ManualStockOut))
            .ToList();

        if (consumptionTx.Count == 0)
        {
            return [];
        }

        var rows = consumptionTx
            .GroupBy(tx => tx.TransactionDate)
            .Select(g =>
            {
                var totalQuantity = g.Sum(tx => tx.Quantity);
                var totalCost = g.Sum(tx => tx.TotalCostBdt);
                var avgRate = totalQuantity > 0 ? Math.Round(totalCost / totalQuantity, 2) : 0m;

                return new ItemWiseConsumptionReportRow(
                    Date: g.Key,
                    ConsumedQuantity: totalQuantity,
                    Unit: unit,
                    UnitCostBdt: avgRate,
                    TotalCostBdt: totalCost);
            })
            .OrderBy(r => r.Date)
            .ToList();

        return rows;
    }
}
