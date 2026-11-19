using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>How long an open bill has been overdue.</summary>
public enum AgeingBucket
{
    Current = 0,
    Days1To30 = 1,
    Days31To60 = 2,
    Days61To90 = 3,
    Over90 = 4,
}

/// <summary>One party's open credit, split by how overdue it is.</summary>
public sealed record TradeAgeingReportRow(
    string Section,
    string Party,
    int Bills,
    decimal CurrentBdt,
    decimal Days1To30Bdt,
    decimal Days31To60Bdt,
    decimal Days61To90Bdt,
    decimal Over90Bdt,
    DateOnly? OldestDueDate,
    int MaxDaysOverdue)
{
    public decimal TotalBdt => CurrentBdt + Days1To30Bdt + Days31To60Bdt + Days61To90Bdt + Over90Bdt;
}

/// <summary>The ageing rules, public so the bucket boundaries are tested rather than described.</summary>
public static class TradeAgeing
{
    /// <summary>
    /// Days past due on <paramref name="asAt"/>. A bill without a due date is due on its bill date
    /// (cash-on-delivery terms), so it starts ageing the day after.
    /// </summary>
    public static int DaysOverdue(DateOnly billDate, DateOnly? dueDate, DateOnly asAt) =>
        asAt.DayNumber - (dueDate ?? billDate).DayNumber;

    /// <summary>Not yet due (or due today) is Current; then 1–30, 31–60, 61–90 and over 90 days overdue.</summary>
    public static AgeingBucket Bucket(int daysOverdue) => daysOverdue switch
    {
        <= 0 => AgeingBucket.Current,
        <= 30 => AgeingBucket.Days1To30,
        <= 60 => AgeingBucket.Days31To60,
        <= 90 => AgeingBucket.Days61To90,
        _ => AgeingBucket.Over90,
    };

    /// <summary>
    /// What was still owed on a bill at the end of <paramref name="asAt"/>: the bill less the payments
    /// dated on or before that day, less adjustments (returns, waivers, write-offs). Adjustments carry
    /// no date of their own, so they are taken as already made — an as-at date in the past can
    /// therefore understate a balance that was later written off, never overstate one.
    /// </summary>
    public static decimal OutstandingAsAt(TradeBill bill, IEnumerable<TradeBillPayment> payments, DateOnly asAt)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(payments);

        if (bill.IsCancelled || bill.BillDate > asAt)
        {
            return 0m;
        }

        var paid = payments.Where(p => p.BillId == bill.Id && p.PaymentDate <= asAt).Sum(p => p.AmountBdt);
        return bill.AmountBdt - paid - bill.AdjustedBdt;
    }
}

/// <summary>
/// Report FN10 — receivables and payables ageing: every credit bill still open on a date, by
/// customer or supplier, in Current / 1–30 / 31–60 / 61–90 / 90+ days overdue buckets.
/// </summary>
/// <remarks>
/// Receivables (owed to the farm) and payables (owed by it) are separate sections with their own
/// sub-totals per bucket; adding the two together means nothing, so there is no grand total.
/// The subject line compares each section with the Accounts Receivable / Payable ledger balance on
/// the same date, so a difference is visible on the page rather than discovered at audit.
/// </remarks>
public sealed class TradeAgeingReportDefinition : ReportDefinition<TradeAgeingReportRow>
{
    private readonly ITradeBillRepository _bills;
    private readonly IFinancialStatementEngine _engine;

    public TradeAgeingReportDefinition(ITradeBillRepository bills, IFinancialStatementEngine engine)
    {
        _bills = bills;
        _engine = engine;
    }

    public override string Key => "finance.trade-ageing";

    public override LocalizedText Title => new("Receivables & Payables Ageing", "প্রাপ্য ও প্রদেয় বয়সভিত্তিক বিশ্লেষণ");

    public override LocalizedText Description => new(
        "Open customer invoices and supplier bills on a date, by party, in Current / 1-30 / 31-60 / 61-90 / 90+ days overdue buckets, reconciled to the ledger.",
        "নির্দিষ্ট তারিখে ক্রেতা ও সরবরাহকারীভিত্তিক বকেয়া বিল — চলতি / ১-৩০ / ৩১-৬০ / ৬১-৯০ / ৯০+ দিন মেয়াদোত্তীর্ণ, খতিয়ানের সাথে মিলানোসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    // Receivables are owed to the farm and payables by it: their sum is not a figure.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("asAt", new LocalizedText("As at", "তারিখ অনুযায়ী"), required: true, @default: "today"),
        ReportParameter.Select(
            "kind",
            new LocalizedText("Show", "দেখান"),
            [
                new ReportSelectOption("all", new LocalizedText("Receivables and payables", "প্রাপ্য ও প্রদেয়")),
                new ReportSelectOption("receivables", new LocalizedText("Receivables only", "শুধু প্রাপ্য")),
                new ReportSelectOption("payables", new LocalizedText("Payables only", "শুধু প্রদেয়")),
            ],
            @default: "all"),
    ];

    public override IReadOnlyList<ReportColumn<TradeAgeingReportRow>> Columns =>
    [
        ReportColumn<TradeAgeingReportRow>.Text("party", new LocalizedText("Customer / Supplier", "ক্রেতা / সরবরাহকারী"), r => r.Party, width: 3f),
        ReportColumn<TradeAgeingReportRow>.WholeNumber("bills", new LocalizedText("Bills", "বিল"), r => r.Bills, widthMm: 14f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("current", new LocalizedText("Current", "চলতি"), r => r.CurrentBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("d1_30", new LocalizedText("1-30 days", "১-৩০ দিন"), r => r.Days1To30Bdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("d31_60", new LocalizedText("31-60 days", "৩১-৬০ দিন"), r => r.Days31To60Bdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("d61_90", new LocalizedText("61-90 days", "৬১-৯০ দিন"), r => r.Days61To90Bdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("d90", new LocalizedText("Over 90 days", "৯০ দিনের বেশি"), r => r.Over90Bdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Money("total", new LocalizedText("Total outstanding", "মোট বকেয়া"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<TradeAgeingReportRow>.Date("oldestDue", new LocalizedText("Oldest due", "প্রাচীনতম মেয়াদ"), r => r.OldestDueDate),
        ReportColumn<TradeAgeingReportRow>.WholeNumber("maxDays", new LocalizedText("Max days overdue", "সর্বোচ্চ মেয়াদোত্তীর্ণ দিন"), r => r.MaxDaysOverdue, widthMm: 20f, aggregate: ReportAggregate.Max),
    ];

    public override ReportGroup<TradeAgeingReportRow>? Group =>
        ReportGroup<TradeAgeingReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals);

        var parts = new List<string>(2);
        if (data.ShowReceivables)
        {
            parts.Add(bn
                ? $"প্রাপ্য {M(data.ReceivablesBdt)} (খতিয়ান {M(data.LedgerReceivablesBdt)})"
                : $"Receivables {M(data.ReceivablesBdt)} (ledger {M(data.LedgerReceivablesBdt)})");
        }

        if (data.ShowPayables)
        {
            parts.Add(bn
                ? $"প্রদেয় {M(data.PayablesBdt)} (খতিয়ান {M(data.LedgerPayablesBdt)})"
                : $"Payables {M(data.PayablesBdt)} (ledger {M(data.LedgerPayablesBdt)})");
        }

        return string.Join(" · ", parts);
    }

    protected override async Task<IReadOnlyList<TradeAgeingReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Rows;
    }

    private sealed record AgeingData(
        IReadOnlyList<TradeAgeingReportRow> Rows,
        bool ShowReceivables,
        bool ShowPayables,
        decimal ReceivablesBdt,
        decimal PayablesBdt,
        decimal LedgerReceivablesBdt,
        decimal LedgerPayablesBdt);

    private Task<AgeingData> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("trade-ageing", async () =>
        {
            var farmId = context.FarmId();
            var asAt = context.Date("asAt");
            var kind = context.Text("kind", "all").ToLowerInvariant();
            var showReceivables = kind is "all" or "receivables";
            var showPayables = kind is "all" or "payables";
            var bn = context.Language == ReportLanguage.Bn;

            var bills = await _bills.GetByFarmIdAsync(farmId, null, includeCancelled: false, cancellationToken).ConfigureAwait(false);
            var payments = (await _bills.GetPaymentsByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false))
                .ToLookup(p => p.BillId);

            var ledger = await _engine.LoadAsync(farmId, asAt, cancellationToken).ConfigureAwait(false);
            var balances = ledger.BalancesAt(asAt);

            var receivables = showReceivables
                ? BuildSection(
                    bills.Where(b => b.Kind == TradeBillKind.Receivable), payments, asAt,
                    bn ? "ক. প্রাপ্য — ক্রেতার কাছে পাওনা" : "A. Receivables — owed to the farm")
                : [];
            var payables = showPayables
                ? BuildSection(
                    bills.Where(b => b.Kind == TradeBillKind.Payable), payments, asAt,
                    bn ? "খ. প্রদেয় — সরবরাহকারীর পাওনা" : "B. Payables — owed by the farm")
                : [];

            return new AgeingData(
                [.. receivables, .. payables],
                showReceivables,
                showPayables,
                receivables.Sum(r => r.TotalBdt),
                payables.Sum(r => r.TotalBdt),
                balances.GetValueOrDefault(AccountChart.ReceivableAccount),
                -balances.GetValueOrDefault(AccountChart.PayableAccount));
        });

    /// <summary>One section's rows, one per party, largest balance first.</summary>
    public static IReadOnlyList<TradeAgeingReportRow> BuildSection(
        IEnumerable<TradeBill> bills,
        ILookup<Guid, TradeBillPayment> payments,
        DateOnly asAt,
        string section)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(payments);

        var open = bills
            .Select(b => (Bill: b, Outstanding: TradeAgeing.OutstandingAsAt(b, payments[b.Id], asAt)))
            .Where(x => x.Outstanding > TradeBill.Tolerance)
            .ToList();

        return open
            .GroupBy(x => x.Bill.PartyName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var buckets = new decimal[5];
                var maxDays = 0;
                DateOnly? oldest = null;
                foreach (var (bill, outstanding) in g)
                {
                    var due = bill.DueDate ?? bill.BillDate;
                    var days = TradeAgeing.DaysOverdue(bill.BillDate, bill.DueDate, asAt);
                    buckets[(int)TradeAgeing.Bucket(days)] += outstanding;
                    maxDays = Math.Max(maxDays, days);
                    oldest = oldest is null || due < oldest ? due : oldest;
                }

                return new TradeAgeingReportRow(
                    section, g.First().Bill.PartyName.Trim(), g.Count(),
                    buckets[0], buckets[1], buckets[2], buckets[3], buckets[4],
                    oldest, maxDays);
            })
            .OrderByDescending(r => r.TotalBdt)
            .ThenBy(r => r.Party, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
