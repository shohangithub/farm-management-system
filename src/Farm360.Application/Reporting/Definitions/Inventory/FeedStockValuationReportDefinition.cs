using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Queries.Reports;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Feeding;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Inventory;

/// <param name="Category">The category in plain words, in the report's language.</param>
/// <param name="MinThreshold">The reorder level; null when none is set.</param>
/// <param name="Status">The stock status in plain words ("Low — reorder").</param>
public sealed record InventoryStockValuationReportRow(
    string ItemName,
    string Category,
    string Unit,
    decimal CurrentStock,
    decimal UnitCostBdt,
    decimal TotalValueBdt,
    decimal? MinThreshold,
    string Status);

/// <summary>
/// Report IN1 — Stock on hand and its value: every stock item, how much is left, what it is worth
/// at average cost, and whether it needs reordering.
/// </summary>
public sealed class FeedStockValuationReportDefinition : ReportDefinition<InventoryStockValuationReportRow>
{
    public override string Key => "inventory.stock-valuation";

    public override LocalizedText Title => new("Stock on Hand & Value", "বর্তমান মজুদ ও মূল্য");

    public override LocalizedText Description => new(
        "How much of each item is in stock now, what it is worth, and which items need reordering.",
        "এখন প্রতিটি উপকরণ কত মজুদ আছে, তার মূল্য কত, এবং কোনগুলো আবার অর্ডার করতে হবে।");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
    ];

    // The category is the group band, so it is not repeated as a column.
    public override IReadOnlyList<ReportColumn<InventoryStockValuationReportRow>> Columns =>
    [
        ReportColumn<InventoryStockValuationReportRow>.Text("name", new LocalizedText("Item", "উপকরণ"), r => r.ItemName, width: 3.0f),
        ReportColumn<InventoryStockValuationReportRow>.Number("stock", new LocalizedText("In stock", "মজুদ আছে"), r => r.CurrentStock, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum, unitOf: r => r.Unit),
        ReportColumn<InventoryStockValuationReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 16f, relative: false),
        ReportColumn<InventoryStockValuationReportRow>.Number("threshold", new LocalizedText("Reorder level", "পুনরায় অর্ডারের সীমা"), r => r.MinThreshold, decimals: 2, widthMm: 24f),
        ReportColumn<InventoryStockValuationReportRow>.Money("rate", new LocalizedText("Average cost (BDT per unit)", "গড় মূল্য (টাকা/একক)"), r => r.UnitCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.None),
        ReportColumn<InventoryStockValuationReportRow>.Money("totalValue", new LocalizedText("Stock value (BDT)", "মজুদের মূল্য (টাকা)"), r => r.TotalValueBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<InventoryStockValuationReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 26f, relative: false),
    ];

    public override ReportGroup<InventoryStockValuationReportRow>? Group =>
        ReportGroup<InventoryStockValuationReportRow>.ByLocalized(
            r => r.Category,
            r => new LocalizedText($"Category: {r.Category}", $"শ্রেণী: {r.Category}"));

    public override LocalizedText? GrandTotalLabel => new("Total stock value", "মোট মজুদ মূল্য");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one stock item as it stands today.",
            "প্রতিটি সারি আজকের অবস্থায় একটি মজুদ উপকরণ।"),
        new("Stock value = in stock × average cost; the average cost is the weighted average of what was paid for the stock.",
            "মজুদের মূল্য = মজুদ × গড় মূল্য; গড় মূল্য হলো মজুদ কিনতে দেওয়া দামের ভারযুক্ত গড়।"),
        new("\"Low — reorder\" means stock has fallen to the reorder level or below; a blank reorder level means none has been set.",
            "\"কম — অর্ডার দিন\" মানে মজুদ পুনরায় অর্ডারের সীমায় বা তার নিচে নেমেছে; সীমা ফাঁকা মানে কোনো সীমা ঠিক করা হয়নি।"),
        new("Quantities are totalled within a category only when every item shares the same unit; values are always totalled.",
            "একটি শ্রেণীর সব উপকরণের একক এক হলে তবেই পরিমাণ যোগ হয়; মূল্য সবসময় যোগ হয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var today = FeedReportText.Day(context.Today);
        var value = data.TotalValuationBdt.ToString("N0", CultureInfo.InvariantCulture);

        return context.Language == ReportLanguage.Bn
            ? $"{today} তারিখের মজুদ · {data.TotalSkusCount}টি উপকরণ · {data.LowStockCount}টি কম, {data.OutOfStockCount}টি শেষ · মোট মূল্য ৳{value}"
            : $"Stock as of {today} · {data.TotalSkusCount} items · {data.LowStockCount} low, {data.OutOfStockCount} out of stock · total value ৳{value}";
    }

    protected override async Task<IReadOnlyList<InventoryStockValuationReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        if (data.Items.Count == 0)
        {
            return [];
        }

        var language = context.Language;

        // Category order as the farm thinks of it (feed first), not alphabetical.
        return data.Items
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(i => new InventoryStockValuationReportRow(
                ItemName: i.Name,
                Category: FeedReportText.ItemCategory(i.Category).For(language),
                Unit: i.UnitOfMeasure,
                CurrentStock: i.CurrentStock,
                UnitCostBdt: i.WeightedAverageCostBdt,
                TotalValueBdt: i.TotalValueBdt,
                MinThreshold: i.ReorderThreshold > 0m ? i.ReorderThreshold : null,
                Status: FeedReportText.StockStatus(i.Status).For(language)))
            .ToList();
    }

    // The subject line and the body both need the valuation; compute it once per run.
    private static Task<InventoryValuationReportDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("stock-valuation", () =>
            context.Send(new GetInventoryValuationReportQuery(context.FarmId()), cancellationToken));
}
