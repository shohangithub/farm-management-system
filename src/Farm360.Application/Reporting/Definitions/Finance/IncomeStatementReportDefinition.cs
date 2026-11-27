using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record IncomeStatementReportRow(
    string Section,
    string Code,
    string Account,
    decimal AmountBdt,
    decimal? PriorAmountBdt);

/// <summary>
/// Report FN8 — Profit &amp; loss statement for a period (default: the financial year to date),
/// with the same period of the previous year. Income is positive and costs negative, so each
/// section sub-totals to its effect on profit and the grand total is the net profit.
/// </summary>
public sealed class IncomeStatementReportDefinition : ReportDefinition<IncomeStatementReportRow>
{
    public override string Key => "finance.income-statement";

    public override LocalizedText Title => new("Profit & Loss Statement", "লাভ-ক্ষতির বিবরণী");

    public override LocalizedText Description => new(
        "Did the farm make a profit? Money earned, minus the cost of the animals sold and of running the farm, for a period — compared with the same period last year.",
        "খামার কি লাভ করেছে? নির্দিষ্ট সময়ে আয় থেকে বিক্রিত পশুর খরচ ও খামার চালানোর খরচ বাদ দিয়ে — গত বছরের একই সময়ের সাথে তুলনাসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("fromDate", new LocalizedText("From (blank = financial year start)", "শুরু (খালি = অর্থবছরের শুরু)"), required: false, @default: null),
        ReportParameter.Date("toDate", new LocalizedText("To", "পর্যন্ত"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<IncomeStatementReportRow>> Columns =>
    [
        ReportColumn<IncomeStatementReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 14f, relative: false),
        ReportColumn<IncomeStatementReportRow>.Text("account", new LocalizedText("Item", "বিবরণ"), r => r.Account, width: 4f),
        ReportColumn<IncomeStatementReportRow>.Money("amount", new LocalizedText("This period (BDT)", "এই সময়ে (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f, aggregate: ReportAggregate.Sum),
        ReportColumn<IncomeStatementReportRow>.Money("prior", new LocalizedText("Same period last year (BDT)", "গত বছর একই সময়ে (টাকা)"), r => r.PriorAmountBdt, decimals: 2, widthMm: 32f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<IncomeStatementReportRow>? Group =>
        ReportGroup<IncomeStatementReportRow>.By(r => r.Section, r => r.Section);

    public override LocalizedText? GrandTotalLabel => new("Net profit / (loss) for the period", "এই সময়ের নিট মুনাফা / (ক্ষতি)");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new(
            "Income is shown as a plus and every cost as a minus, so each section's sub-total is how much it added to or took from profit.",
            "আয় যোগ (+) এবং প্রতিটি খরচ বিয়োগ (−) হিসেবে দেখানো হয়েছে, তাই প্রতিটি অংশের উপ-মোট দেখায় সেটি মুনাফায় কত যোগ বা বিয়োগ করেছে।"),
        new(
            "The bottom line is net profit: all income minus all costs. A minus figure there is a loss.",
            "শেষ লাইনটি নিট মুনাফা: মোট আয় বাদ মোট খরচ। সেখানে ঋণাত্মক অঙ্ক মানে ক্ষতি।"),
        new(
            "Gross profit (under the title) is income minus the direct cost of the animals sold — the purchase price plus the feed, medicine and labour that raised them.",
            "মোট মুনাফা (শিরোনামের নিচে) হলো আয় বাদ বিক্রিত পশুর সরাসরি খরচ — ক্রয়মূল্য এবং তাদের লালনপালনের খাদ্য, ঔষধ ও শ্রম।"),
        new(
            "Buying animals or stock is not a cost on its own: an animal's purchase price becomes a cost only when it is sold or dies, and feed only when it is used.",
            "পশু বা মালামাল কেনা নিজে খরচ নয়: পশুর ক্রয়মূল্য খরচ হয় যখন পশুটি বিক্রি হয় বা মারা যায়, আর খাদ্য খরচ হয় যখন তা ব্যবহার হয়।"),
        new(
            "Feed, health, labour and overhead spent on animals still on the farm are carried as part of their value (the \"Feed & care of animals still on the farm\" line) and become a cost when the animal is sold or dies.",
            "খামারে থাকা পশুর খাদ্য, চিকিৎসা, শ্রম ও অন্যান্য খরচ তাদের মূল্যের অংশ হিসেবে পরবর্তী সময়ে নেওয়া হয় (\"খামারে থাকা পশুর খাদ্য ও যত্নের খরচ\" লাইন) এবং পশু বিক্রি বা মারা গেলে তা খরচ হয়।"),
        new(
            "Loans received and owner or investor money are not income, and loan repayments are not costs — only the interest is.",
            "গৃহীত ঋণ এবং মালিক বা বিনিয়োগকারীর অর্থ আয় নয়, আর ঋণের আসল পরিশোধ খরচ নয় — শুধু সুদ খরচ।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var t = data.Totals;
        string M(decimal v) => StatementWording.Money(context, v);
        var margin = StatementWording.Number(context, t.GrossMarginPercent.ToString("0.#", CultureInfo.InvariantCulture));
        var period = $"{StatementWording.Day(context, data.FromDate)} – {StatementWording.Day(context, data.ToDate)}";

        if (context.Language == ReportLanguage.Bn)
        {
            var resultBn = t.NetProfitBdt >= 0 ? "নিট মুনাফা" : "নিট ক্ষতি";
            return $"{period} · আয় {M(t.RevenueBdt)} · মোট মুনাফা {M(t.GrossProfitBdt)} (আয়ের {margin}%) · {resultBn} {M(Math.Abs(t.NetProfitBdt))} টাকা";
        }

        var result = t.NetProfitBdt >= 0 ? "Net profit" : "Net loss";
        return $"{period} · Income {M(t.RevenueBdt)} · Gross profit {M(t.GrossProfitBdt)} ({margin}% of income) · {result} {M(Math.Abs(t.NetProfitBdt))} BDT";
    }

    protected override async Task<IReadOnlyList<IncomeStatementReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;

        var rows = new List<IncomeStatementReportRow>();
        var number = 0;
        foreach (var section in data.Sections)
        {
            if (section.Lines.Count == 0)
            {
                continue;
            }

            number++;
            var sign = section.Key == "revenue" ? 1m : -1m;
            var label = SectionLabel(section, bn);
            var prefix = bn ? ReportValueFormatter.ToBengaliNumerals(number.ToString(CultureInfo.InvariantCulture)) : number.ToString(CultureInfo.InvariantCulture);
            rows.AddRange(section.Lines.Select(l => new IncomeStatementReportRow(
                $"{prefix}. {label}",
                l.AccountCode,
                bn && !string.IsNullOrWhiteSpace(l.AccountNameBn) ? l.AccountNameBn : l.AccountName,
                sign * l.AmountBdt,
                l.PriorAmountBdt is { } p ? sign * p : null)));
        }

        return rows;
    }

    private static string SectionLabel(IncomeStatementSectionDto section, bool bn) => section.Key switch
    {
        "revenue" => bn ? "আয় (বিক্রয় ও অন্যান্য আয়)" : "Income (sales and other income)",
        "cost-of-sales" => bn ? "বিক্রিত পশুর সরাসরি খরচ (বিক্রীত পণ্যের ব্যয়)" : "Direct cost of animals sold (cost of sales)",
        "operating-expenses" => bn ? "খামার চালানোর অন্যান্য খরচ (পরিচালন ব্যয়)" : "Other running costs (operating expenses)",
        "finance-costs" => bn ? "ঋণের সুদ (আর্থিক ব্যয়)" : "Loan interest (finance costs)",
        _ => bn ? section.NameBn : section.Name,
    };

    // The subject line and the body both need the statement; load it once per run.
    private static Task<IncomeStatementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("income-statement", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<IncomeStatementDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        DateTime? from = context.Has("fromDate") ? context.Date("fromDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
        var to = context.Date("toDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await context.Send(new GetIncomeStatementQuery(farmId, from, to), cancellationToken).ConfigureAwait(false);
    }
}
