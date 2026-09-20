using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record BatchPnlReportRow(
    string LineItem,
    string Category,
    decimal AmountBdt,
    string Notes);

/// <summary>
/// Report FN4 — Batch Profit and Loss performance statement showing revenue, accumulated cost,
/// and net margin per cattle batch.
/// </summary>
public sealed class BatchPnlReportDefinition : ReportDefinition<BatchPnlReportRow>
{
    public override string Key => "finance.batch-pnl";

    public override LocalizedText Title => new("Batch Profit & Loss Statement", "ব্যাচ লাভ ও ক্ষতি বিবরণী");

    public override LocalizedText Description => new(
        "Revenues, accumulated costs, gross profit, and return on investment for an individual batch.",
        "একটি নির্দিষ্ট ব্যাচের রাজস্ব, মোট অর্জিত খরচ, মোট লাভ ও বিনিয়োগের ওপর অর্জিত লাভ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Batch("batchId", required: true),
        ReportParameter.Farm(),
    ];

    public override IReadOnlyList<ReportColumn<BatchPnlReportRow>> Columns =>
    [
        ReportColumn<BatchPnlReportRow>.Text("lineItem", new LocalizedText("Economic Metric", "আর্থিক সূচক"), r => r.LineItem, width: 3.2f),
        ReportColumn<BatchPnlReportRow>.Text("category", new LocalizedText("Classification", "শ্রেণী"), r => r.Category, width: 22f, relative: false),
        ReportColumn<BatchPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f),
        ReportColumn<BatchPnlReportRow>.Text("notes", new LocalizedText("Notes / Formula", "মন্তব্য / বিবরণ"), r => r.Notes, width: 3.0f),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"Batch Financial Audit Statement";
    }

    protected override async Task<IReadOnlyList<BatchPnlReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var batchId = context.Id("batchId");

        var data = await context
            .Send(new GetBatchPnLReportQuery(farmId, batchId), cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            return [];
        }

        return
        [
            new BatchPnlReportRow("Total Cattle Enrolled", "Operational", data.TotalAnimals, $"{data.TotalAnimals} head of cattle in batch"),
            new BatchPnlReportRow("Batch Realized Revenue", "Revenue", data.TotalIncomeBdt, "Total income recognized from animal & milk sales"),
            new BatchPnlReportRow("Total Accumulated Cost", "Expenditure", data.TotalCostBdt, "Direct feed, healthcare, overhead, and acquisition cost"),
            new BatchPnlReportRow("Net Operating Profit / Loss", "Margin", data.GrossProfitBdt, "Gross margin = Realized Revenue - Total Cost"),
            new BatchPnlReportRow("Return on Investment (ROI %)", "Profitability", data.ReturnOnInvestmentPercent, "ROI % = (Net Profit / Total Cost) * 100"),
        ];
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
