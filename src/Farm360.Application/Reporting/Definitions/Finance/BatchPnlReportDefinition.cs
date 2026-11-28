using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Livestock.Queries;
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

    public override LocalizedText Title => new("Batch Profit or Loss", "ব্যাচের লাভ বা ক্ষতি");

    public override LocalizedText Description => new(
        "Did this batch make money? Money earned from the batch against everything it cost, with an estimate that includes the animals still on the farm.",
        "এই ব্যাচে লাভ হয়েছে কি? ব্যাচ থেকে আয় বনাম মোট খরচ, এবং খামারে থাকা পশুসহ আনুমানিক ফলাফল।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Part 1 counts only money actually received; part 2 adds what the animals still on the farm are worth today.",
            "অংশ ১-এ শুধু আসলে পাওয়া টাকা ধরা হয়েছে; অংশ ২-এ খামারে থাকা পশুর আজকের আনুমানিক মূল্যও যোগ করা হয়েছে।"),
        new("Total cost covers every animal in the batch, sold or not: purchase price, feed, vet & medicine, and its share of labour and overhead.",
            "মোট খরচে ব্যাচের সব পশু ধরা হয়েছে, বিক্রি হোক বা না হোক: ক্রয়মূল্য, খাদ্য, চিকিৎসা ও ওষুধ, এবং শ্রম ও পরোক্ষ খরচের অংশ।"),
        new("So part 1 usually shows a loss until most of the batch is sold; part 2 is the expected outcome.",
            "তাই ব্যাচের বেশিরভাগ পশু বিক্রি না হওয়া পর্যন্ত অংশ ১-এ সাধারণত ক্ষতি দেখায়; অংশ ২ হলো প্রত্যাশিত ফলাফল।"),
        new("Return on cost = profit ÷ total cost. Animals still on the farm are valued using the farm's valuation setting.",
            "খরচের ওপর লাভ = লাভ ÷ মোট খরচ। খামারে থাকা পশুর মূল্য খামারের মূল্যায়ন নিয়ম অনুযায়ী ধরা হয়েছে।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch("batchId", required: true),
    ];

    public override IReadOnlyList<ReportColumn<BatchPnlReportRow>> Columns =>
    [
        ReportColumn<BatchPnlReportRow>.Text("lineItem", new LocalizedText("Item", "বিবরণ"), r => r.LineItem, width: 3.2f),
        ReportColumn<BatchPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f),
        ReportColumn<BatchPnlReportRow>.Text("metric", new LocalizedText("Head / return on cost", "পশু / খরচের ওপর লাভ"), r => r.Metric, width: 34f, relative: false, align: ReportAlign.Right),
        ReportColumn<BatchPnlReportRow>.Text("notes", new LocalizedText("How it is worked out", "যেভাবে হিসাব করা"), r => r.Notes, width: 3.0f),
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

        var batch = await context
            .Send(new GetBatchDetailsQuery(context.Id("batchId")), cancellationToken)
            .ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        var name = string.IsNullOrWhiteSpace(batch?.Name) ? null : batch!.Name;
        var batchText = bn
            ? (name is null ? "ব্যাচ" : "ব্যাচ " + name)
            : (name is null ? "Batch" : "Batch " + name);

        return bn
            ? $"{batchText} · মোট {Whole(context, data.TotalAnimals)} টি পশু, খামারে আছে {Whole(context, data.OnHandAnimals)} টি · আজ পর্যন্ত"
            : $"{batchText} · {data.TotalAnimals} head in batch, {data.OnHandAnimals} still on the farm · as of today";
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
        string Roi(decimal ratio) => bn
            ? "খরচের ওপর " + Percent(context, ratio)
            : Percent(context, ratio) + " return on cost";

        var realised = T("1. Money actually received so far", "১. এ পর্যন্ত আসলে পাওয়া টাকা");
        var estimated = T("2. Including animals still on the farm (estimate)", "২. খামারে থাকা পশুসহ (আনুমানিক)");
        var perHeadText = string.Empty;
        if (data.TotalAnimals > 0)
        {
            var perHead = decimal.Round(data.TotalCostBdt / data.TotalAnimals, 2, MidpointRounding.AwayFromZero);
            perHeadText = bn ? Money(context, perHead) + " টাকা/টি" : Money(context, perHead) + " BDT/head";
        }

        return
        [
            new(realised, T("Money earned from the batch", "ব্যাচ থেকে আয়"), data.TotalIncomeBdt, string.Empty,
                T("Animal sales and any other income booked to this batch or its animals", "পশু বিক্রয় ও এই ব্যাচ বা এর পশুর নামে লেখা অন্যান্য আয়")),
            new(realised, T("Less: total cost of the batch", "বাদ: ব্যাচের মোট খরচ"), data.TotalCostBdt, perHeadText,
                T("Purchase, feed, vet & medicine, labour and overhead of every animal in the batch", "ব্যাচের প্রতিটি পশুর ক্রয়, খাদ্য, চিকিৎসা, শ্রম ও পরোক্ষ খরচ")),
            new(realised, T("Profit / (loss) so far", "এ পর্যন্ত লাভ / (ক্ষতি)"), data.GrossProfitBdt, Roi(data.ReturnOnInvestmentPercent),
                T("Money earned − total cost", "আয় − মোট খরচ")),

            new(estimated, T("Value of animals still on the farm", "খামারে থাকা পশুর আনুমানিক মূল্য"), data.UnrealisedValueBdt,
                bn ? Whole(context, data.OnHandAnimals) + " টি" : Whole(context, data.OnHandAnimals) + " head",
                ValueNote(context, data)),
            new(estimated, T("Expected profit / (loss)", "প্রত্যাশিত লাভ / (ক্ষতি)"), data.EstimatedTotalProfitBdt, Roi(data.EstimatedRoiPercent),
                T("Money earned + value on the farm − total cost", "আয় + খামারে থাকা পশুর মূল্য − মোট খরচ")),
        ];
    }

    /// <summary>How the on-farm value was reached, naming the animals valued at cost because they have no weight yet.</summary>
    private static string ValueNote(ReportContext context, BatchPnLReportDto data)
    {
        var bn = context.Language == ReportLanguage.Bn;
        var note = bn
            ? "খামারের মূল্যায়ন দরে সক্রিয় ও পৃথক রাখা পশু"
            : "Active and quarantined animals at the farm's valuation rate";
        if (data.UnweighedAtCostAnimals <= 0)
        {
            return note;
        }

        return bn
            ? $"{note}; {Whole(context, data.UnweighedAtCostAnimals)} টি ক্রয়মূল্যে ধরা (এখনো ওজন নেই): {Money(context, data.UnweighedAtCostValueBdt)} টাকা"
            : $"{note}; {data.UnweighedAtCostAnimals} valued at cost (no weight yet): {Money(context, data.UnweighedAtCostValueBdt)} BDT";
    }

    private static Task<BatchPnLReportDto?> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("batch-pnl", async () =>
            (BatchPnLReportDto?)await context
                .Send(new GetBatchPnLReportQuery(context.FarmId(), context.Id("batchId")), cancellationToken)
                .ConfigureAwait(false));

    private static string Percent(ReportContext context, decimal ratio) =>
        ReportValueFormatter.Format(ratio, ReportColumnType.Percent, 1, context.BengaliNumerals) + "%";

    private static string Money(ReportContext context, decimal value) =>
        ReportValueFormatter.Format(value, ReportColumnType.Money, 2, context.BengaliNumerals);

    private static string Whole(ReportContext context, int value) =>
        ReportValueFormatter.Format(value, ReportColumnType.WholeNumber, 0, context.BengaliNumerals);
}
