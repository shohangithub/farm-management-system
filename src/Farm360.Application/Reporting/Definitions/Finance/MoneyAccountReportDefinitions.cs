using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record MoneyAccountCashBookReportRow(
    DateTime Date,
    string VoucherNo,
    string Particulars,
    string Head,
    decimal? InBdt,
    decimal? OutBdt,
    decimal BalanceBdt);

/// <summary>
/// Report FN14 — Cash book of one money account (cash in hand, a bank account, a mobile wallet):
/// opening balance, every receipt and payment with its running balance, closing balance.
/// </summary>
public sealed class MoneyAccountCashBookReportDefinition : ReportDefinition<MoneyAccountCashBookReportRow>
{
    public override string Key => "finance.money-account-cash-book";

    public override LocalizedText Title => new("Money Account Cash Book", "টাকার খাতার নগদান বই");

    public override LocalizedText Description => new(
        "Every taka that came into or went out of one money account (cash in hand, bank or mobile wallet), with the running balance.",
        "একটি টাকার খাতায় (হাতে নগদ, ব্যাংক বা মোবাইল ওয়ালেট) কত টাকা এসেছে ও গেছে, চলতি জেরসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.MoneyAccount("accountId", new LocalizedText("Money account", "টাকার খাতা")),
        ReportParameter.DateRange("period", "current-month"),
    ];

    // In and out add up to the period's movement; the balance column is a running figure, never summed.
    public override IReadOnlyList<ReportColumn<MoneyAccountCashBookReportRow>> Columns =>
    [
        ReportColumn<MoneyAccountCashBookReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => DateOnly.FromDateTime(r.Date)),
        ReportColumn<MoneyAccountCashBookReportRow>.Text("voucher", new LocalizedText("Voucher", "ভাউচার"), r => r.VoucherNo, width: 22f, relative: false),
        ReportColumn<MoneyAccountCashBookReportRow>.Text("particulars", new LocalizedText("Particulars", "বিবরণ"), r => r.Particulars, width: 3f),
        ReportColumn<MoneyAccountCashBookReportRow>.Text("head", new LocalizedText("Head", "খাত"), r => r.Head, width: 2f),
        ReportColumn<MoneyAccountCashBookReportRow>.Money("in", new LocalizedText("In (BDT)", "জমা (টাকা)"), r => r.InBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<MoneyAccountCashBookReportRow>.Money("out", new LocalizedText("Out (BDT)", "খরচ (টাকা)"), r => r.OutBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<MoneyAccountCashBookReportRow>.Money("balance", new LocalizedText("Balance (BDT)", "জের (টাকা)"), r => r.BalanceBdt, widthMm: 26f),
    ];

    public override LocalizedText? GrandTotalLabel => new("Movement in the period", "এই সময়ের লেনদেন");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Opening balance + in − out = closing balance. Transfers between the farm's own accounts appear in both accounts and do not change the farm's total cash.",
            "প্রারম্ভিক জের + জমা − খরচ = সমাপনী জের। খামারের নিজের খাতাগুলোর মধ্যে স্থানান্তর দুই খাতাতেই দেখা যায়, খামারের মোট নগদ বদলায় না।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => StatementWording.Money(context, v);
        var period = $"{StatementWording.Day(context, s.FromDate)} – {StatementWording.Day(context, s.ToDate)}";
        var name = s.Account.Name == Farm360.Domain.Finance.MoneyAccount.DefaultName && context.Language == ReportLanguage.Bn ? "হাতে নগদ" : s.Account.Name;
        return StatementWording.Pick(context,
            $"{name} · {period} · Opening {M(s.OpeningBdt)} · Closing {M(s.ClosingBdt)} BDT",
            $"{name} · {period} · প্রারম্ভিক জের {M(s.OpeningBdt)} · সমাপনী জের {M(s.ClosingBdt)} টাকা");
    }

    protected override async Task<IReadOnlyList<MoneyAccountCashBookReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        var rows = new List<MoneyAccountCashBookReportRow>
        {
            new(s.FromDate.ToDateTime(TimeOnly.MinValue), "—", bn ? "প্রারম্ভিক জের" : "Opening balance", string.Empty, null, null, s.OpeningBdt),
        };
        rows.AddRange(s.Lines.Select(l => new MoneyAccountCashBookReportRow(
            l.Date,
            l.VoucherNo,
            l.IsTransfer && l.CounterAccountName is { } other
                ? (l.InBdt > 0 ? (bn ? $"স্থানান্তর: {other} থেকে" : $"Transfer from {other}") : (bn ? $"স্থানান্তর: {other}-এ" : $"Transfer to {other}"))
                : l.Description,
            bn ? l.CategoryNameBn : l.CategoryName,
            l.InBdt == 0m ? null : l.InBdt,
            l.OutBdt == 0m ? null : l.OutBdt,
            l.BalanceBdt)));
        return rows;
    }

    private static Task<MoneyAccountStatementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("money-account-statement", () =>
        {
            var range = context.Range("period");
            return context.Send(new GetMoneyAccountStatementQuery(context.FarmId(), context.IdOrNull("accountId"), range.From, range.To), cancellationToken);
        });
}

public sealed record PartnerStatementReportRow(
    DateTime Date,
    string Entry,
    decimal? InBdt,
    decimal? OutBdt,
    decimal CapitalAfterBdt,
    string Account,
    string Notes);

/// <summary>
/// Report FN15 — A partner's statement: money put in and taken out, profit allocated (by
/// capital-days) and paid, current capital and share, and their part of the farm's net assets.
/// </summary>
public sealed class PartnerStatementReportDefinition : ReportDefinition<PartnerStatementReportRow>
{
    public override string Key => "finance.partner-statement";

    public override LocalizedText Title => new("Partner Statement", "অংশীদারের হিসাব বিবরণী");

    public override LocalizedText Description => new(
        "One partner's capital in and out, profit allocated by capital-days and paid, share of the farm and part of its net assets.",
        "একজন অংশীদারের মূলধন জমা ও উত্তোলন, মূলধন-দিনের ভিত্তিতে পাওয়া ও পরিশোধিত লাভ, খামারে অংশ এবং নিট সম্পদে তাঁর ভাগ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Partner(),
        ReportParameter.DateRange("period", "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<PartnerStatementReportRow>> Columns =>
    [
        ReportColumn<PartnerStatementReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => DateOnly.FromDateTime(r.Date)),
        ReportColumn<PartnerStatementReportRow>.Text("entry", new LocalizedText("Entry", "বিবরণ"), r => r.Entry, width: 2f),
        ReportColumn<PartnerStatementReportRow>.Money("in", new LocalizedText("Put in / allocated (BDT)", "জমা / প্রাপ্য (টাকা)"), r => r.InBdt, widthMm: 26f),
        ReportColumn<PartnerStatementReportRow>.Money("out", new LocalizedText("Taken out / paid (BDT)", "উত্তোলন / পরিশোধ (টাকা)"), r => r.OutBdt, widthMm: 26f),
        ReportColumn<PartnerStatementReportRow>.Money("capital", new LocalizedText("Capital after (BDT)", "এরপর মূলধন (টাকা)"), r => r.CapitalAfterBdt, widthMm: 26f),
        ReportColumn<PartnerStatementReportRow>.Text("account", new LocalizedText("Money account", "টাকার খাতা"), r => r.Account, width: 1.4f),
        ReportColumn<PartnerStatementReportRow>.Text("notes", new LocalizedText("Notes", "মন্তব্য"), r => r.Notes, width: 2f),
    ];

    // Capital, profit allocated and payouts are different things; one total down the page would mix them.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Capital = money put in − capital taken out. Share = this partner's capital ÷ all partners' capital.",
            "মূলধন = জমা দেওয়া টাকা − তুলে নেওয়া মূলধন। অংশ = এই অংশীদারের মূলধন ÷ সব অংশীদারের মোট মূলধন।"),
        new("Profit is split by capital-days: each partner's capital × the days it was in the farm during the period. Someone who joined half-way through gets half the weight for the same money.",
            "লাভ ভাগ হয় মূলধন-দিনে: প্রত্যেক অংশীদারের মূলধন × সেই সময়ে কত দিন টাকাটা খামারে ছিল। মাঝপথে যোগ দিলে একই টাকার জন্য অর্ধেক ভার পান।"),
        new("Net assets = animals + stock + cash & bank + money owed to the farm + fixed assets − loans and other debts, at book value (cost), not market price.",
            "নিট সম্পদ = পশু + মজুদ + নগদ ও ব্যাংক + খামারের পাওনা + স্থায়ী সম্পদ − ঋণ ও অন্যান্য দেনা, খাতা মূল্যে (ক্রয়মূল্যে), বাজারদরে নয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => StatementWording.Money(context, v);
        var period = $"{StatementWording.Day(context, s.FromDate)} – {StatementWording.Day(context, s.ToDate)}";
        var share = StatementWording.Number(context, s.Partner.SharePercent.ToString("0.##", CultureInfo.InvariantCulture));
        return StatementWording.Pick(context,
            $"{s.Partner.Name} · {period} · Capital {M(s.Partner.CapitalBdt)} BDT · Share {share}%",
            $"{s.Partner.Name} · {period} · মূলধন {M(s.Partner.CapitalBdt)} টাকা · অংশ {share}%");
    }

    protected override async Task<IReadOnlyList<PartnerStatementReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        var rows = new List<PartnerStatementReportRow>
        {
            new(s.FromDate.ToDateTime(TimeOnly.MinValue), bn ? "শুরুর মূলধন" : "Capital at start", null, null, s.OpeningCapitalBdt, string.Empty, string.Empty),
        };
        foreach (var l in s.Lines)
        {
            var (label, isIn) = l.Type switch
            {
                "CapitalContribution" => (bn ? "মূলধন জমা" : "Capital put in", true),
                "CapitalWithdrawal" => (bn ? "মূলধন উত্তোলন" : "Capital taken out", false),
                "ProfitDistribution" => (bn ? "লাভ পরিশোধ" : "Profit paid out", false),
                "ProfitAllocation" => (bn ? "লাভের ভাগ (মূলধন-দিন অনুযায়ী)" : "Profit allocated (capital-days)", true),
                _ => (l.Type, true),
            };
            var account = l.MoneyAccountName == Farm360.Domain.Finance.MoneyAccount.DefaultName && bn ? "হাতে নগদ" : l.MoneyAccountName ?? string.Empty;
            rows.Add(new PartnerStatementReportRow(l.Date, label, isIn ? l.AmountBdt : null, isIn ? null : l.AmountBdt,
                l.CapitalAfterBdt, account,
                l.Type == "ProfitAllocation"
                    ? (bn
                        ? $"{StatementWording.Day(context, s.FromDate)} – {StatementWording.Day(context, s.ToDate)} সময়ের লাভের ভাগ, মূলধন-দিন অনুযায়ী"
                        : $"Share of profit {StatementWording.Day(context, s.FromDate)} – {StatementWording.Day(context, s.ToDate)} by capital-days")
                    : l.Notes ?? string.Empty));
        }

        return rows;
    }

    protected override async Task<ReportSummary?> SummarizeAsync(ReportContext context, IReadOnlyList<PartnerStatementReportRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var s = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        string M(decimal v) => StatementWording.Money(context, v);
        string P(decimal v) => StatementWording.Number(context, v.ToString("0.##", CultureInfo.InvariantCulture)) + "%";
        var p = s.Partner;
        var mine = s.Allocation.Lines.FirstOrDefault(l => l.PartnerId == p.Id);

        var figures = new List<ReportSummaryFigure>
        {
            new(bn ? "বর্তমান মূলধন" : "Capital now", M(p.CapitalBdt)),
            new(bn ? "খামারে অংশ" : "Share of the farm", P(p.SharePercent)),
            new(bn ? "এই সময়ের লাভের ভাগ" : "Profit allocated this period", M(mine?.AllocatedBdt ?? 0m), (mine?.AllocatedBdt ?? 0m) < 0 ? ReportTone.Bad : ReportTone.Good),
            new(bn ? "মোট পাওনা লাভ" : "Profit due (all time)", M(p.ProfitDueBdt)),
            new(bn ? "নিট সম্পদে ভাগ" : "Part of net assets", M(s.PartnerNetAssetsBdt)),
        };

        var insights = s.NetAssets
            .Where(l => l.FarmBdt != 0m)
            .Select(l => bn
                ? $"{l.NameBn}: খামারের {M(l.FarmBdt)} টাকা — এর মধ্যে {p.Name}-এর ভাগ {M(l.PartnerBdt)} টাকা"
                : $"{l.Name}: farm {M(l.FarmBdt)} BDT — {p.Name}'s part {M(l.PartnerBdt)} BDT")
            .ToList();
        if (mine is not null)
        {
            insights.Insert(0, bn
                ? $"এই সময়ে গড় মূলধন {M(mine.AverageCapitalBdt)} টাকা, মূলধন-দিনে ভাগ {P(mine.SharePercent)}; বণ্টনযোগ্য লাভ {M(s.Allocation.DistributableProfitBdt)} টাকা।"
                : $"Average capital in the period {M(mine.AverageCapitalBdt)} BDT, capital-day share {P(mine.SharePercent)} of a distributable profit of {M(s.Allocation.DistributableProfitBdt)} BDT.");
        }

        return new ReportSummary(bn ? "সারসংক্ষেপ" : "Summary", figures, bn ? "নিট সম্পদে অংশ" : "Share of the farm's net assets", insights);
    }

    private static Task<PartnerStatementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("partner-statement", () =>
        {
            var range = context.Range("period");
            return context.Send(new GetPartnerStatementQuery(context.FarmId(), context.Id("partnerId"), range.From, range.To), cancellationToken);
        });
}
