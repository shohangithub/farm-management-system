using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record TrialBalanceReportRow(
    string AccountCode,
    string AccountName,
    string Category,
    decimal DebitBdt,
    decimal CreditBdt);

/// <summary>
/// Report FN1 — Trial balance: every account's balance on a date on its debit or credit side,
/// grouped as assets, liabilities, equity, income and expenses. Total debits must equal total credits.
/// </summary>
public sealed class TrialBalanceReportDefinition : ReportDefinition<TrialBalanceReportRow>
{
    public override string Key => "finance.trial-balance";

    public override LocalizedText Title => new("Trial Balance", "রেওয়ামিল");

    public override LocalizedText Description => new(
        "Do the books add up? Every account's balance on a date, on its debit or credit side — total debits must equal total credits.",
        "হিসাব কি মিলছে? নির্দিষ্ট তারিখে প্রতিটি হিসাবের ডেবিট বা ক্রেডিট উদ্বৃত্ত — মোট ডেবিট ও মোট ক্রেডিট সমান হতে হবে।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("asOfDate", new LocalizedText("As of Date", "তারিখ অনুযায়ী"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<TrialBalanceReportRow>> Columns =>
    [
        ReportColumn<TrialBalanceReportRow>.Text("code", new LocalizedText("Account code", "হিসাব কোড"), r => r.AccountCode, width: 20f, relative: false),
        ReportColumn<TrialBalanceReportRow>.Text("name", new LocalizedText("Account", "হিসাবের নাম"), r => r.AccountName, width: 3.0f),
        ReportColumn<TrialBalanceReportRow>.Money("debit", new LocalizedText("Debit (BDT)", "ডেবিট (টাকা)"), r => r.DebitBdt, decimals: 2, widthMm: 30f, aggregate: ReportAggregate.Sum),
        ReportColumn<TrialBalanceReportRow>.Money("credit", new LocalizedText("Credit (BDT)", "ক্রেডিট (টাকা)"), r => r.CreditBdt, decimals: 2, widthMm: 30f, aggregate: ReportAggregate.Sum),
    ];

    // The account class is the group band, so it is not repeated as a column.
    public override ReportGroup<TrialBalanceReportRow>? Group =>
        ReportGroup<TrialBalanceReportRow>.ByLocalized(r => r.Category, r => ClassLabel(r.Category));

    // Debits and credits are each totalled separately; the two totals must be equal.
    public override LocalizedText? GrandTotalLabel => new(
        "Total — debits must equal credits",
        "সর্বমোট — ডেবিট ও ক্রেডিট সমান হতে হবে");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new(
            "Each account appears once, with its balance on the date in either the Debit or the Credit column.",
            "প্রতিটি হিসাব একবার এসেছে; তারিখের উদ্বৃত্ত ডেবিট বা ক্রেডিট — যেকোনো এক কলামে।"),
        new(
            "Debit side: what the farm owns (assets) and what it spent (expenses). Credit side: what it owes (liabilities), the owners' money (equity) and what it earned (income).",
            "ডেবিট দিকে: খামারের সম্পদ ও খরচ। ক্রেডিট দিকে: দায়, মালিকদের অর্থ (মালিকানা স্বত্ব) ও আয়।"),
        new(
            "The bottom line must show the same total in both columns; the line under the title says whether it does.",
            "শেষ লাইনে দুই কলামের মোট সমান হতে হবে; শিরোনামের নিচের লাইনে তা মিলেছে কি না দেখানো হয়েছে।"),
        new(
            "A difference usually means the opening balances entered in Accounts setup are incomplete; it equals the balance sheet's difference.",
            "গরমিল থাকলে সাধারণত হিসাব সেটআপে দেওয়া প্রারম্ভিক জের অসম্পূর্ণ; এটি উদ্বৃত্তপত্রের গরমিলের সমান।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => StatementWording.Money(context, v);
        var day = StatementWording.Day(context, context.Date("asOfDate"));

        if (context.Language == ReportLanguage.Bn)
        {
            return data.IsBalanced
                ? $"{day} তারিখে · মোট ডেবিট = মোট ক্রেডিট = {M(data.TotalDebitBdt)} টাকা — হিসাব মিলেছে"
                : $"{day} তারিখে · গরমিল {M(data.DifferenceBdt)} টাকা (ডেবিট {M(data.TotalDebitBdt)}, ক্রেডিট {M(data.TotalCreditBdt)}) — প্রারম্ভিক জের দেখুন";
        }

        return data.IsBalanced
            ? $"As at {day} · Total debits = total credits = {M(data.TotalDebitBdt)} BDT — balanced"
            : $"As at {day} · DIFFERENCE {M(data.DifferenceBdt)} BDT (debits {M(data.TotalDebitBdt)}, credits {M(data.TotalCreditBdt)}) — check the opening balances";
    }

    protected override async Task<IReadOnlyList<TrialBalanceReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        if (data.Lines.Count == 0)
        {
            return [];
        }

        var bn = context.Language == ReportLanguage.Bn;
        return data.Lines
            .OrderBy(l => ClassOrder(l.CategoryGroup))
            .ThenBy(l => l.AccountCode, StringComparer.Ordinal)
            .Select(l => new TrialBalanceReportRow(
                AccountCode: l.AccountCode,
                AccountName: bn ? AccountChart.Account(l.AccountCode).NameBn : l.AccountName,
                Category: l.CategoryGroup,
                DebitBdt: l.DebitBdt,
                CreditBdt: l.CreditBdt))
            .ToList();
    }

    /// <summary>Statement order: balance-sheet classes first, then the profit &amp; loss ones.</summary>
    private static int ClassOrder(string accountClass) => accountClass switch
    {
        nameof(AccountClass.Asset) => 0,
        nameof(AccountClass.Liability) => 1,
        nameof(AccountClass.Equity) => 2,
        nameof(AccountClass.Revenue) => 3,
        nameof(AccountClass.Expense) => 4,
        _ => 5,
    };

    private static LocalizedText ClassLabel(string accountClass) => accountClass switch
    {
        nameof(AccountClass.Asset) => new("1. Assets — what the farm owns", "১. সম্পদ — খামারের যা আছে"),
        nameof(AccountClass.Liability) => new("2. Liabilities — what the farm owes", "২. দায় — খামারের যা দেনা"),
        nameof(AccountClass.Equity) => new("3. Equity — owner's and investors' money", "৩. মালিকানা স্বত্ব — মালিক ও বিনিয়োগকারীদের অর্থ"),
        nameof(AccountClass.Revenue) => new("4. Income", "৪. আয়"),
        nameof(AccountClass.Expense) => new("5. Expenses", "৫. খরচ"),
        _ => new(accountClass, accountClass),
    };

    // The subject line and the body both need the trial balance; load it once per run.
    private static Task<TrialBalanceDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("trial-balance", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<TrialBalanceDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        var asOfDate = context.Date("asOfDate").ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return await context
            .Send(new GetTrialBalanceQuery(farmId, asOfDate), cancellationToken)
            .ConfigureAwait(false);
    }
}
