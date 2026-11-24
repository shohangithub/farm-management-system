using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record IncomeExpenseReportRow(
    string Section,
    DateTime? Date,
    string VoucherNo,
    string Head,
    string Description,
    decimal? ReceiptBdt,
    decimal? PaymentBdt);

/// <summary>
/// Report FN6 — Daily / date-range Income &amp; Expense statement in classic two-sided cash-book form:
/// opening balance + receipts on one side, payments + closing balance on the other, so the two
/// column totals always agree.
/// </summary>
public sealed class IncomeExpenseReportDefinition : ReportDefinition<IncomeExpenseReportRow>
{
    public override string Key => "finance.income-expense";

    public override LocalizedText Title => new("Cash In & Cash Out (Cash Book)", "নগদ আয় ও ব্যয় (ক্যাশ বই)");

    public override LocalizedText Description => new(
        "Where the farm's cash came from and where it went: cash at the start, every amount received and paid, and cash left at the end.",
        "খামারের নগদ টাকা কোথা থেকে এল ও কোথায় গেল: শুরুর নগদ, প্রতিটি প্রাপ্তি ও পরিশোধ, এবং শেষে হাতে থাকা নগদ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each line is one amount of cash received (Money in) or paid out (Money out), listed by date.",
            "প্রতিটি সারি একটি নগদ প্রাপ্তি (টাকা এসেছে) বা পরিশোধ (টাকা গেছে), তারিখ অনুযায়ী।"),
        new("Cash at the end = cash at the start + money in − money out, so the two bottom totals are always equal.",
            "শেষের নগদ = শুরুর নগদ + টাকা এসেছে − টাকা গেছে, তাই নিচের দুটি মোট সবসময় সমান।"),
        new("This is a cash view: a credit sale shows only when the buyer pays, and a credit purchase only when the supplier is paid.",
            "এটি নগদ হিসাব: বাকিতে বিক্রি তখনই দেখায় যখন ক্রেতা টাকা দেয়, আর বাকিতে কেনা তখনই দেখায় যখন সরবরাহকারীকে টাকা দেওয়া হয়।"),
        new("Loans, owner money and buying animals or stock move cash but are not profit or loss — see the Monthly Profit or Loss report for that.",
            "ঋণ, মালিকের টাকা এবং পশু বা মজুদ কেনায় নগদ নড়ে কিন্তু তা লাভ-ক্ষতি নয় — তার জন্য মাসিক লাভ বা ক্ষতি প্রতিবেদন দেখুন।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<IncomeExpenseReportRow>> Columns =>
    [
        ReportColumn<IncomeExpenseReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<IncomeExpenseReportRow>.Text("voucher", new LocalizedText("Voucher no.", "ভাউচার নং"), r => r.VoucherNo, width: 26f, relative: false),
        ReportColumn<IncomeExpenseReportRow>.Text("head", new LocalizedText("Category", "খাত"), r => r.Head, width: 1.6f),
        ReportColumn<IncomeExpenseReportRow>.Text("description", new LocalizedText("Details", "বিবরণ"), r => r.Description, width: 2.6f),
        ReportColumn<IncomeExpenseReportRow>.Money("receipt", new LocalizedText("Money in (BDT)", "টাকা এসেছে (টাকা)"), r => r.ReceiptBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<IncomeExpenseReportRow>.Money("payment", new LocalizedText("Money out (BDT)", "টাকা গেছে (টাকা)"), r => r.PaymentBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<IncomeExpenseReportRow>? Group =>
        ReportGroup<IncomeExpenseReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var range = context.Range("period");
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var period = AnimalCostReportText.Period(range);
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals);

        return context.Language == ReportLanguage.Bn
            ? $"{period} · শুরুর নগদ {M(data.OpeningBalanceBdt)} · এসেছে {M(data.TotalIncomeBdt)} · গেছে {M(data.TotalExpenseBdt)} · শেষের নগদ {M(data.ClosingBalanceBdt)} টাকা"
            : $"{period} · Cash at start {M(data.OpeningBalanceBdt)} · in {M(data.TotalIncomeBdt)} · out {M(data.TotalExpenseBdt)} · cash at end {M(data.ClosingBalanceBdt)} BDT";
    }

    protected override async Task<IReadOnlyList<IncomeExpenseReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;
        string Head(CashBookLineDto t) => bn && !string.IsNullOrWhiteSpace(t.HeadNameBn) ? t.HeadNameBn : t.HeadName;
        static string OrDash(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();

        var opening = T("1. Cash at the start", "১. শুরুর নগদ");
        var income = T("2. Money in", "২. টাকা এসেছে");
        var expense = T("3. Money out", "৩. টাকা গেছে");
        var closing = T("4. Cash at the end", "৪. শেষের নগদ");

        var rows = new List<IncomeExpenseReportRow>
        {
            new(opening, data.FromDate, "—", T("Opening cash", "প্রারম্ভিক নগদ"),
                T("Cash in hand at the close of the previous day", "আগের দিন শেষে হাতে থাকা নগদ"), data.OpeningBalanceBdt, null),
        };

        foreach (var t in data.IncomeTransactions.OrderBy(x => x.TransactionDate).ThenBy(x => x.VoucherNo, StringComparer.Ordinal))
        {
            rows.Add(new IncomeExpenseReportRow(income, t.TransactionDate, OrDash(t.VoucherNo),
                Head(t), OrDash(t.Description), t.AmountBdt, null));
        }

        foreach (var t in data.ExpenseTransactions.OrderBy(x => x.TransactionDate).ThenBy(x => x.VoucherNo, StringComparer.Ordinal))
        {
            rows.Add(new IncomeExpenseReportRow(expense, t.TransactionDate, OrDash(t.VoucherNo),
                Head(t), OrDash(t.Description), null, t.AmountBdt));
        }

        rows.Add(new IncomeExpenseReportRow(closing, data.ToDate, "—", T("Closing cash", "সমাপনী নগদ"),
            T("Cash in hand at the close of the last day", "শেষ দিন শেষে হাতে থাকা নগদ"), null, data.ClosingBalanceBdt));

        return rows;
    }

    /// <summary>Opening + receipts equals payments + closing, so both column totals agree.</summary>
    public override LocalizedText? GrandTotalLabel => new(
        "Totals (cash at start + money in = money out + cash at end)",
        "মোট (শুরুর নগদ + টাকা এসেছে = টাকা গেছে + শেষের নগদ)");

    // The subject line and the body both need the cash book; load it once per run.
    private static Task<IncomeExpenseReportDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("income-expense", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<IncomeExpenseReportDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        var range = context.Range("period");
        return await context.Send(
            new GetIncomeExpenseReportQuery(
                farmId,
                range.From.ToDateTime(TimeOnly.MinValue),
                range.To.ToDateTime(TimeOnly.MinValue)),
            cancellationToken).ConfigureAwait(false);
    }
}

