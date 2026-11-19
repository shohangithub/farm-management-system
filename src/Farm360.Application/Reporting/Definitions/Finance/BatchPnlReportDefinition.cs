using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>
/// One line of the batch statement. <paramref name="AmountBdt"/> is money only; head counts and
/// ratios go in <paramref name="Metric"/>, so no column ever mixes taka with heads or percent.
/// </summary>
public sealed record BatchPnlReportRow(
    string Section,
    string LineItem,
    decimal? AmountBdt,
    string Metric,
    string Notes);

/// <summary>
/// Report FN4 — Batch Profit and Loss statement: revenue, accumulated cost and the realised and
/// estimated result for one cattle batch.
/// </summary>
/// <remarks>
/// A statement, not a register: the result lines are explicit rows ("Realised profit / (loss)"),
/// and there is no grand total — adding revenue to cost to profit to ROI produces a number that
/// is none of those things.
/// </remarks>
public sealed class BatchPnlReportDefinition : ReportDefinition<BatchPnlReportRow>
{
    public override string Key => "finance.batch-pnl";

    public override LocalizedText Title => new("Batch Profit & Loss Statement", "ব্যাচ লাভ ও ক্ষতি বিবরণী");

    public override LocalizedText Description => new(
        "Revenues, accumulated costs, gross profit, and return on investment for an individual batch.",
        "একটি নির্দিষ্ট ব্যাচের রাজস্ব, মোট অর্জিত খরচ, মোট লাভ ও বিনিয়োগের ওপর অর্জিত লাভ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch("batchId", required: true),
    ];

    public override IReadOnlyList<ReportColumn<BatchPnlReportRow>> Columns =>
    [
        ReportColumn<BatchPnlReportRow>.Text("lineItem", new LocalizedText("Particulars", "বিবরণ"), r => r.LineItem, width: 3.2f),
        ReportColumn<BatchPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f),
        ReportColumn<BatchPnlReportRow>.Text("metric", new LocalizedText("Measure", "পরিমাপ"), r => r.Metric, width: 24f, relative: false, align: ReportAlign.Right),
        ReportColumn<BatchPnlReportRow>.Text("notes", new LocalizedText("Notes / Basis", "মন্তব্য / ভিত্তি"), r => r.Notes, width: 3.0f),
    ];

    public override ReportGroup<BatchPnlReportRow>? Group =>
        ReportGroup<BatchPnlReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            return null;
        }

        return context.Language == ReportLanguage.Bn
            ? $"মোট {Whole(context, data.TotalAnimals)} টি পশু, খামারে আছে {Whole(context, data.OnHandAnimals)} টি"
            : $"{data.TotalAnimals} head in batch, {data.OnHandAnimals} still on the farm";
    }

    protected override async Task<IReadOnlyList<BatchPnlReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        if (data is null)
        {
            return [];
        }

        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;

        var realised = T("1. Realised result", "১. অর্জিত ফলাফল");
        var estimated = T("2. Including animals still on hand", "২. খামারে থাকা পশুসহ আনুমানিক ফলাফল");

        return
        [
            new(realised, T("Batch revenue", "ব্যাচের আয়"), data.TotalIncomeBdt, string.Empty,
                T("Income from animal sales and other batch revenue", "পশু বিক্রয় ও ব্যাচের অন্যান্য আয়")),
            new(realised, T("Less: total accumulated cost", "বাদ: মোট খরচ"), data.TotalCostBdt, string.Empty,
                T("Acquisition, feed, healthcare, labour and overhead of every animal", "ক্রয়, খাদ্য, চিকিৎসা, শ্রম ও পরোক্ষ খরচ")),
            new(realised, T("Realised profit / (loss)", "অর্জিত লাভ / (ক্ষতি)"), data.GrossProfitBdt,
                Percent(context, data.ReturnOnInvestmentPercent) + " ROI",
                T("Revenue − total cost; ROI = profit ÷ cost", "আয় − মোট খরচ; ROI = লাভ ÷ খরচ")),

            new(estimated, T("Unrealised value of on-hand animals", "খামারে থাকা পশুর আনুমানিক মূল্য"), data.UnrealisedValueBdt,
                T($"{Whole(context, data.OnHandAnimals)} head", $"{Whole(context, data.OnHandAnimals)} টি"),
                T("Active and quarantined animals at the farm's valuation rate", "খামারের মূল্যায়ন দরে সক্রিয় ও পৃথক রাখা পশু")),
            new(estimated, T("Estimated total profit / (loss)", "আনুমানিক মোট লাভ / (ক্ষতি)"), data.EstimatedTotalProfitBdt,
                Percent(context, data.EstimatedRoiPercent) + " ROI",
                T("Revenue + unrealised value − total cost", "আয় + আনুমানিক মূল্য − মোট খরচ")),
        ];
    }

    private static Task<BatchPnLReportDto?> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("batch-pnl", async () =>
            (BatchPnLReportDto?)await context
                .Send(new GetBatchPnLReportQuery(context.FarmId(), context.Id("batchId")), cancellationToken)
                .ConfigureAwait(false));

    private static string Percent(ReportContext context, decimal ratio) =>
        ReportValueFormatter.Format(ratio, ReportColumnType.Percent, 2, context.BengaliNumerals) + "%";

    private static string Whole(ReportContext context, int value) =>
        ReportValueFormatter.Format(value, ReportColumnType.WholeNumber, 0, context.BengaliNumerals);
}
