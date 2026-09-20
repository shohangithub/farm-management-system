using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Inventory.Queries.Reports;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Inventory;

public sealed record InventoryStockValuationReportRow(
    string ItemName,
    string Category,
    string Unit,
    decimal CurrentStock,
    decimal UnitCostBdt,
    decimal TotalValueBdt,
    decimal MinThreshold,
    string Status);

/// <summary>
/// Report IN1 — Comprehensive Inventory Stock Valuation and Reorder Status Statement.
/// </summary>
public sealed class FeedStockValuationReportDefinition : ReportDefinition<InventoryStockValuationReportRow>
{
    public override string Key => "inventory.stock-valuation";

    public override LocalizedText Title => new("Inventory Stock Valuation Report", "মজুদ মূল্যায়ন ও স্টক অবস্থা প্রতিবেদন");

    public override LocalizedText Description => new(
        "Current stock levels, weighted average costs, total valuation, and reorder thresholds by category.",
        "বিভাগভিত্তিক বর্তমান মজুদের পরিমাণ, গড় ক্রয়মূল্য, মোট আর্থিক মূল্যায়ন ও পুনঃঅর্ডার সীমা প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Inventory;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
    ];

    public override IReadOnlyList<ReportColumn<InventoryStockValuationReportRow>> Columns =>
    [
        ReportColumn<InventoryStockValuationReportRow>.Text("name", new LocalizedText("Item Name", "পণ্য / খাদ্য উপাদান"), r => r.ItemName, width: 2.8f),
        ReportColumn<InventoryStockValuationReportRow>.Text("category", new LocalizedText("Category", "শ্রেণী"), r => r.Category, width: 2.0f),
        ReportColumn<InventoryStockValuationReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<InventoryStockValuationReportRow>.Number("stock", new LocalizedText("Current Stock", "বর্তমান স্টক"), r => r.CurrentStock, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<InventoryStockValuationReportRow>.Money("rate", new LocalizedText("Avg. Rate (BDT)", "গড় দর (টাকা)"), r => r.UnitCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.None),
        ReportColumn<InventoryStockValuationReportRow>.Money("totalValue", new LocalizedText("Total Value (BDT)", "মোট মূল্য (টাকা)"), r => r.TotalValueBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<InventoryStockValuationReportRow>.Number("threshold", new LocalizedText("Min. Threshold", "সর্বনিম্ন সীমা"), r => r.MinThreshold, decimals: 2, widthMm: 22f),
        ReportColumn<InventoryStockValuationReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 18f, relative: false),
    ];

    public override ReportGroup<InventoryStockValuationReportRow>? Group =>
        ReportGroup<InventoryStockValuationReportRow>.By(
            r => r.Category,
            r => $"Category: {r.Category}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return "Farm Inventory Stock Valuation & Reorder Thresholds";
    }

    protected override async Task<IReadOnlyList<InventoryStockValuationReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);

        var data = await context
            .Send(new GetInventoryValuationReportQuery(farmId), cancellationToken)
            .ConfigureAwait(false);

        if (data?.Items == null || data.Items.Count == 0)
        {
            return [];
        }

        return data.Items
            .Select(i => new InventoryStockValuationReportRow(
                ItemName: i.Name,
                Category: i.Category.ToString(),
                Unit: i.UnitOfMeasure,
                CurrentStock: i.CurrentStock,
                UnitCostBdt: i.WeightedAverageCostBdt,
                TotalValueBdt: i.TotalValueBdt,
                MinThreshold: i.ReorderThreshold,
                Status: i.Status.ToString()))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.ItemName)
            .ToList();
    }

    private static async Task<Guid> ResolveFarmIdAsync(ReportContext context, CancellationToken cancellationToken)
    {
        if (context.IdOrNull("farmId") is { } id && id != Guid.Empty)
            return id;
        if (context.AssignedFarmIds is { Count: > 0 } assigned && assigned[0] != Guid.Empty)
            return assigned[0];

        var farms = await context.Send(new GetAllFarmsQuery(), cancellationToken).ConfigureAwait(false);
        return farms.Count > 0 ? farms[0].Id : Guid.Empty;
    }
}
