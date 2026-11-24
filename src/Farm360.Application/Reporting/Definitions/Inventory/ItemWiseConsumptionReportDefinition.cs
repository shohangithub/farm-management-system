using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Feeding;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Reporting.Definitions.Inventory;

public sealed record ItemWiseConsumptionReportRow(
    DateOnly Date,
    decimal ConsumedQuantity,
    string Unit,
    decimal UnitCostBdt,
    decimal TotalCostBdt);

/// <summary>
/// Report IN3 — Usage of one stock item: day by day, how much of the chosen item was taken out of
/// stock and what it cost, sub-totalled by month.
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

    public override LocalizedText Title => new("Item Usage by Day", "উপকরণের দৈনিক ব্যবহার");

    public override LocalizedText Description => new(
        "How much of one stock item was used each day in the period, and what it cost.",
        "নির্বাচিত সময়ে একটি মজুদ উপকরণ প্রতিদিন কত ব্যবহার হয়েছে এবং কত খরচ হয়েছে।");

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
        ReportColumn<ItemWiseConsumptionReportRow>.Number("quantity", new LocalizedText("Quantity used", "ব্যবহৃত পরিমাণ"), r => r.ConsumedQuantity, decimals: 2, widthMm: 35f, aggregate: ReportAggregate.Sum, unitOf: r => r.Unit),
        ReportColumn<ItemWiseConsumptionReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 22f, relative: false),
        ReportColumn<ItemWiseConsumptionReportRow>.Money("rate", new LocalizedText("Average rate (BDT per unit)", "গড় দর (টাকা/একক)"), r => r.UnitCostBdt, decimals: 2, widthMm: 38f, aggregate: ReportAggregate.None),
        ReportColumn<ItemWiseConsumptionReportRow>.Money("totalCost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.TotalCostBdt, decimals: 2, widthMm: 40f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<ItemWiseConsumptionReportRow>? Group =>
        ReportGroup<ItemWiseConsumptionReportRow>.ByLocalized(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => FeedReportText.Month(r.Date));

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "পুরো সময়ের মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one day on which this item was taken out of stock; days with no use are not listed.",
            "প্রতিটি সারি এমন একটি দিন যেদিন এই উপকরণ মজুদ থেকে বের হয়েছে; যেদিন ব্যবহার হয়নি সেদিন তালিকায় নেই।"),
        new("Average rate = cost ÷ quantity used that day, at the stock's average purchase price.",
            "গড় দর = খরচ ÷ সেদিনের ব্যবহৃত পরিমাণ, মজুদের গড় ক্রয়মূল্যে।"),
        new("Monthly sub-totals and the period total add up quantity and cost; rates are not added.",
            "মাসিক উপ-মোট ও পুরো সময়ের মোটে পরিমাণ ও খরচ যোগ হয়; দর যোগ হয় না।"),
        new("Feed put back into stock is not counted as used; write-offs, count adjustments and returns to the supplier are not included.",
            "মজুদে ফেরত আসা খাদ্য ব্যবহার হিসেবে ধরা হয় না; নষ্ট হিসেবে বাদ, গণনার সমন্বয় ও সরবরাহকারীকে ফেরত এতে নেই।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = FeedReportText.Period(context.Range("period"));
        var selectedItemId = context.Id("inventoryItemId");

        var item = await _itemRepository.GetByIdAsync(selectedItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return period;
        }

        var category = FeedReportText.ItemCategory(item.Category).For(context.Language);
        return context.Language == ReportLanguage.Bn
            ? $"{item.Name} · {category} · একক: {item.UnitOfMeasure} · {period}"
            : $"{item.Name} · {category} · unit: {item.UnitOfMeasure} · {period}";
    }

    protected override async Task<IReadOnlyList<ItemWiseConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var period = context.Range("period");
        var selectedItemId = context.Id("inventoryItemId");

        var item = await _itemRepository.GetByIdAsync(selectedItemId, cancellationToken).ConfigureAwait(false);
        var unit = item?.UnitOfMeasure ?? "unit";

        var consumptionTx = await StockUsageTransactions
            .LoadAsync(_transactionRepository, farmId, period, selectedItemId, cancellationToken)
            .ConfigureAwait(false);

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
