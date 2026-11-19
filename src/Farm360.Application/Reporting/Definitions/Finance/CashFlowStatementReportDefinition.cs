using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>Where a cash movement belongs on the statement of cash flows.</summary>
public enum CashFlowActivity
{
    Operating = 0,
    Investing = 1,
    Financing = 2,
}

/// <summary>One line of the statement: the net cash moved by one account head in the period.</summary>
public sealed record CashFlowLine(CashFlowActivity Activity, string Code, string Name, string NameBn, decimal AmountBdt);

/// <summary>
/// The cash flow statement for a period, built from the same ledger as the balance sheet so the
/// opening and closing cash are, by construction, the balance sheet's cash on those days.
/// </summary>
/// <param name="OpeningCashBdt">Cash &amp; bank (1010) at the close of the day before the period.</param>
/// <param name="ClosingCashBdt">Cash &amp; bank (1010) at the close of the last day — the balance sheet figure.</param>
/// <param name="UnexplainedBdt">
/// Closing − opening − net movement. Zero unless something other than a cash posting touched the
/// cash account inside the period (an opening entry dated mid-period); printed when it is not.
/// </param>
public sealed record CashFlowStatement(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<CashFlowLine> Lines,
    decimal OpeningCashBdt,
    decimal ClosingCashBdt)
{
    public decimal NetChangeBdt => Lines.Sum(l => l.AmountBdt);

    public decimal UnexplainedBdt => ClosingCashBdt - OpeningCashBdt - NetChangeBdt;

    public decimal NetFor(CashFlowActivity activity) => Lines.Where(l => l.Activity == activity).Sum(l => l.AmountBdt);

    /// <summary>
    /// Operating: everything that runs the farm — sales, every expense, buying cattle and stock for
    /// fattening, paying suppliers and collecting from customers, interest. Investing: sheds, machinery
    /// and other fixed assets bought or sold. Financing: loans, investor and owner money.
    /// </summary>
    public static CashFlowActivity Classify(TransactionCategory category) => category switch
    {
        TransactionCategory.FixedAssetPurchase or TransactionCategory.AssetDisposal => CashFlowActivity.Investing,
        _ => AccountChart.For(category).Group is AccountChart.Financing or AccountChart.Capital
            ? CashFlowActivity.Financing
            : CashFlowActivity.Operating,
    };

    /// <summary>
    /// Builds the statement from cash-affecting postings only (<c>AffectsCash</c>: settled through
    /// Cash &amp; Bank). Credit deals reach it when the bill is paid, as a supplier payment or a
    /// customer collection — never on the deal date.
    /// </summary>
    public static CashFlowStatement Build(FarmLedger ledger, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var opening = ledger.OpeningBalancesFor(from).GetValueOrDefault(AccountChart.CashAccount);
        var closing = ledger.BalancesAt(to).GetValueOrDefault(AccountChart.CashAccount);

        var lines = ledger.PostingsBetween(from, to)
            .Where(t => t.AffectsCash)
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                var head = AccountChart.For(g.Key);
                return new CashFlowLine(Classify(g.Key), head.Code, head.Name, head.NameBn, g.Sum(AccountChart.CashEffect));
            })
            .Where(l => l.AmountBdt != 0m)
            .OrderBy(l => l.Activity)
            .ThenBy(l => l.AmountBdt < 0m) // receipts before payments
            .ThenBy(l => l.Code, StringComparer.Ordinal)
            .ToList();

        return new CashFlowStatement(from, to, lines, opening, closing);
    }
}

public sealed record CashFlowReportRow(
    string Section,
    string Code,
    string Particulars,
    decimal? FlowBdt,
    decimal? BalanceBdt);

/// <summary>
/// Report FN11 — Statement of Cash Flows (direct method): cash received and paid in the period by
/// operating, investing and financing activity, reconciled from opening to closing cash.
/// </summary>
/// <remarks>
/// <para>
/// Built from the financial statement engine's ledger (<see cref="FarmLedger"/>), the same books as
/// the balance sheet, trial balance and full-accounts report. Opening cash is
/// <see cref="FarmLedger.OpeningBalancesFor"/> and closing cash <see cref="FarmLedger.BalancesAt"/>,
/// so both equal the balance sheet's Cash &amp; Bank and the cash book on those dates.
/// </para>
/// <para>
/// Flows go in their own column and balances in another, so the grand total of the flow column is
/// exactly the net increase in cash and no balance is ever added to a flow.
/// </para>
/// </remarks>
public sealed class CashFlowStatementReportDefinition : ReportDefinition<CashFlowReportRow>
{
    private readonly IFinancialStatementEngine _engine;

    public CashFlowStatementReportDefinition(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public override string Key => "finance.cash-flow";

    public override LocalizedText Title => new("Statement of Cash Flows", "নগদ প্রবাহ বিবরণী");

    public override LocalizedText Description => new(
        "Cash received and paid by operating, investing and financing activity, from opening to closing cash — reconciled to the balance sheet and cash book.",
        "পরিচালন, বিনিয়োগ ও অর্থায়ন কার্যক্রমে নগদ প্রাপ্তি ও প্রদান — প্রারম্ভিক থেকে সমাপনী নগদ, উদ্বৃত্তপত্র ও নগদ বইয়ের সাথে মিলানো।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "fiscal-year-to-date"),
    ];

    public override IReadOnlyList<ReportColumn<CashFlowReportRow>> Columns =>
    [
        ReportColumn<CashFlowReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<CashFlowReportRow>.Text("particulars", new LocalizedText("Particulars", "বিবরণ"), r => r.Particulars, width: 4f),
        ReportColumn<CashFlowReportRow>.Money("flow", new LocalizedText("Cash flow (BDT)", "নগদ প্রবাহ (টাকা)"), r => r.FlowBdt, widthMm: 32f, aggregate: ReportAggregate.Sum),
        ReportColumn<CashFlowReportRow>.Money("balance", new LocalizedText("Cash balance (BDT)", "নগদ স্থিতি (টাকা)"), r => r.BalanceBdt, widthMm: 32f),
    ];

    public override ReportGroup<CashFlowReportRow>? Group =>
        ReportGroup<CashFlowReportRow>.By(r => r.Section, r => r.Section);

    public override LocalizedText? GrandTotalLabel => new("Net increase / (decrease) in cash", "নগদের নিট বৃদ্ধি / (হ্রাস)");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals);

        return context.Language == ReportLanguage.Bn
            ? $"প্রারম্ভিক নগদ {M(s.OpeningCashBdt)} · নিট পরিবর্তন {M(s.NetChangeBdt)} · সমাপনী নগদ {M(s.ClosingCashBdt)}"
            : $"Opening cash {M(s.OpeningCashBdt)} · net change {M(s.NetChangeBdt)} · closing cash {M(s.ClosingCashBdt)}";
    }

    protected override async Task<IReadOnlyList<CashFlowReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return ToRows(s, context.Language);
    }

    /// <summary>The printed lines: one section per activity, then the cash reconciliation.</summary>
    public static IReadOnlyList<CashFlowReportRow> ToRows(CashFlowStatement statement, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var bn = language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;

        var sections = new Dictionary<CashFlowActivity, string>
        {
            [CashFlowActivity.Operating] = T("1. Cash flows from operating activities", "১. পরিচালন কার্যক্রম থেকে নগদ প্রবাহ"),
            [CashFlowActivity.Investing] = T("2. Cash flows from investing activities", "২. বিনিয়োগ কার্যক্রম থেকে নগদ প্রবাহ"),
            [CashFlowActivity.Financing] = T("3. Cash flows from financing activities", "৩. অর্থায়ন কার্যক্রম থেকে নগদ প্রবাহ"),
        };

        var rows = new List<CashFlowReportRow>();
        foreach (var (activity, section) in sections)
        {
            var lines = statement.Lines.Where(l => l.Activity == activity).ToList();
            if (lines.Count == 0)
            {
                rows.Add(new CashFlowReportRow(section, string.Empty, T("No cash movement", "কোনো নগদ লেনদেন নেই"), 0m, null));
                continue;
            }

            rows.AddRange(lines.Select(l => new CashFlowReportRow(
                section,
                l.Code,
                (l.AmountBdt >= 0 ? T("Received: ", "প্রাপ্তি: ") : T("Paid: ", "প্রদান: ")) + (bn ? l.NameBn : l.Name),
                l.AmountBdt,
                null)));
        }

        var reconciliation = T("4. Cash reconciliation", "৪. নগদ মিলকরণ");
        var cash = AccountChart.CashAccount;
        rows.Add(new CashFlowReportRow(reconciliation, cash, T($"Opening cash & bank ({statement.From:dd-MMM-yyyy})", $"প্রারম্ভিক নগদ ও ব্যাংক ({statement.From:dd-MMM-yyyy})"), null, statement.OpeningCashBdt));
        rows.Add(new CashFlowReportRow(reconciliation, string.Empty, T("Net increase / (decrease) in cash", "নগদের নিট বৃদ্ধি / (হ্রাস)"), null, statement.NetChangeBdt));
        rows.Add(new CashFlowReportRow(reconciliation, cash, T($"Closing cash & bank ({statement.To:dd-MMM-yyyy}) — balance sheet", $"সমাপনী নগদ ও ব্যাংক ({statement.To:dd-MMM-yyyy}) — উদ্বৃত্তপত্র"), null, statement.ClosingCashBdt));

        if (Math.Abs(statement.UnexplainedBdt) >= 0.01m)
        {
            rows.Add(new CashFlowReportRow(
                reconciliation, string.Empty,
                T("Difference not explained by cash postings (check opening entries)", "নগদ লেনদেনে ব্যাখ্যাহীন পার্থক্য (প্রারম্ভিক এন্ট্রি দেখুন)"),
                null, statement.UnexplainedBdt));
        }

        return rows;
    }

    private Task<CashFlowStatement> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("cash-flow", async () =>
        {
            var range = context.Range("period");
            var ledger = await _engine.LoadAsync(context.FarmId(), range.To, cancellationToken).ConfigureAwait(false);
            return CashFlowStatement.Build(ledger, range.From, range.To);
        });
}
