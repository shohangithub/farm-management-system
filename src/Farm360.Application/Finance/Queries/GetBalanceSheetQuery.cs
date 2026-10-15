using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Accounting;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <param name="CompareDate">Comparative date; defaults to the previous financial year end. Pass <see cref="DateTime.MinValue"/> for no comparison.</param>
public record GetBalanceSheetQuery(
    Guid FarmId,
    DateTime? AsOfDate = null,
    DateTime? CompareDate = null
) : IRequest<BalanceSheetDto>;

/// <summary>
/// Statement of financial position, in the auditor's layout: non-current and current assets;
/// current liabilities; capital, drawings and retained earnings split into brought forward and
/// the current financial year's profit.
/// </summary>
/// <remarks>
/// Built from <see cref="IFinancialStatementEngine"/>: every posting after the books start, the
/// opening balances, year-end adjustments, the fixed-asset register and its depreciation. Opening
/// balances are one-sided and nothing is plugged, so an incomplete opening position shows up as a
/// <c>Difference</c> rather than being hidden. Whether each account is <i>right</i> is shown by the
/// reconciliations against the registers (stock, herd, loans, investors, credit bills).
/// </remarks>
public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IFinancialStatementEngine _engine;
    private readonly IAccountingReconciliationService _reconciliation;

    public GetBalanceSheetQueryHandler(IFinancialStatementEngine engine, IAccountingReconciliationService reconciliation)
    {
        _engine = engine;
        _reconciliation = reconciliation;
    }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var asOfDateTime = request.AsOfDate ?? DateTime.UtcNow;
        var asOf = DateOnly.FromDateTime(asOfDateTime);

        var ledger = await _engine.LoadAsync(request.FarmId, asOf, cancellationToken);
        var fyStart = FiscalYear.StartOf(asOf, ledger.FiscalYearStartMonth);

        DateOnly? priorDate = request.CompareDate switch
        {
            null => fyStart.AddDays(-1),
            { } d when d == DateTime.MinValue => null,
            { } d => DateOnly.FromDateTime(d),
        };
        // Before the books start there is no position to compare with.
        if (priorDate is { } p && (p >= asOf || (ledger.BooksStartDate is { } bs && p < bs)))
            priorDate = null;

        var current = BalanceSheetPosition.Build(ledger, asOf);
        var prior = priorDate is { } pd ? BalanceSheetPosition.Build(ledger, pd) : null;

        var assets = Section("Assets", BalanceSheetPosition.AssetLayout, current, prior);
        var liabilities = Section("Liabilities", BalanceSheetPosition.LiabilityLayout, current, prior);
        var equity = Section("Equity", BalanceSheetPosition.EquityLayout, current, prior);

        var difference = R(current.TotalAssets - current.TotalLiabilitiesAndEquity);

        var reconciliations = asOfDateTime.Date >= DateTime.UtcNow.Date
            // Registers are current snapshots, so the comparison is only meaningful for today.
            ? await _reconciliation.ReconcileAsync(request.FarmId, ledger.AllPostings.ToList(), cancellationToken)
            : [];

        return new BalanceSheetDto(
            request.FarmId,
            asOfDateTime,
            assets,
            liabilities,
            equity,
            R(current.TotalAssets),
            R(current.TotalLiabilitiesAndEquity),
            difference == 0m,
            difference,
            R(current.CurrentAssets - current.CurrentLiabilities))
        {
            CurrentYearProfitBdt = R(current.CurrentYearProfit),
            RetainedEarningsBroughtForwardBdt = R(current.RetainedBroughtForward),
            Reconciliations = reconciliations,
            PriorDate = priorDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            PriorTotalAssetsBdt = prior is null ? null : R(prior.TotalAssets),
            PriorTotalLiabilitiesAndEquityBdt = prior is null ? null : R(prior.TotalLiabilitiesAndEquity),
            PriorDifferenceBdt = prior is null ? null : R(prior.TotalAssets - prior.TotalLiabilitiesAndEquity),
            BooksStartDate = ledger.BooksStartDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            FiscalYearStart = fyStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            FiscalYearStartMonth = ledger.FiscalYearStartMonth,
            IsConfigured = ledger.IsConfigured,
            Message = Explain(ledger, difference),
        };
    }

    private static string? Explain(FarmLedger ledger, decimal difference)
    {
        if (difference != 0m)
        {
            return ledger.BooksStartDate is { } start
                ? $"Assets and liabilities + equity differ by {difference:N2} BDT. The opening balances entered for {start:dd-MMM-yyyy} do not balance — add the missing opening asset, liability or capital so the opening position is complete."
                : $"Assets and liabilities + equity differ by {difference:N2} BDT. Check the opening balances in Accounts setup.";
        }

        return ledger.IsConfigured
            ? null
            : "Accounts setup not done: the statement runs from the first posting on a July–June financial year. Set the books start date, opening balances and fixed assets in Finance → Accounts setup.";
    }

    private static BalanceSheetSectionDto Section(
        string name,
        IReadOnlyList<BalanceSheetPosition.LineSpec> layout,
        BalanceSheetPosition current,
        BalanceSheetPosition? prior)
    {
        var lines = new List<BalanceSheetLineDto>();
        foreach (var spec in layout)
        {
            var amount = current.Amount(spec.Code);
            var priorAmount = prior?.Amount(spec.Code);
            if (!spec.AlwaysShow && amount == 0m && (priorAmount ?? 0m) == 0m)
                continue;

            lines.Add(new BalanceSheetLineDto(spec.Code, spec.Name, R(amount), spec.Notes)
            {
                Group = spec.Group,
                NameBn = spec.NameBn,
                PriorAmountBdt = priorAmount is { } pa ? R(pa) : null,
                IsDeduction = spec.IsDeduction,
            });
        }

        // Amounts on deduction lines are already negative, so the section total is a plain sum.
        return new BalanceSheetSectionDto(name, R(lines.Sum(l => l.AmountBdt)), lines)
        {
            PriorTotalBdt = prior is null ? null : R(lines.Sum(l => l.PriorAmountBdt ?? 0m)),
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 2);
}

/// <summary>The balance-sheet amount of every line at one date, from a <see cref="FarmLedger"/>.</summary>
public sealed class BalanceSheetPosition
{
    public const string NonCurrent = "Non-current";
    public const string Current = "Current";
    public const string CapitalGroup = "Capital";
    public const string ReservesGroup = "Reserves";

    // Presentation-only lines (amounts reclassified from another account's wrong-side balance).
    public const string CustomerAdvancesLine = "2150";
    public const string OverdraftLine = "2020";

    public sealed record LineSpec(string Code, string Name, string NameBn, string Group, string? Notes, bool AlwaysShow = false, bool IsDeduction = false);

    public static readonly IReadOnlyList<LineSpec> AssetLayout =
    [
        new(AccountChart.FixedAssetAccount, "Property, Plant & Equipment (cost)", "স্থায়ী সম্পদ (ক্রয়মূল্য)", NonCurrent,
            "Sheds, machinery, vehicles and equipment at cost, from the fixed-asset register"),
        new(AccountChart.AccumulatedDepreciationAccount, "Less: Accumulated Depreciation", "বাদ: পুঞ্জীভূত অবচয়", NonCurrent,
            "Depreciation charged to date", IsDeduction: true),
        new(AccountChart.CashAccount, "Cash & Bank", "নগদ ও ব্যাংক", Current,
            "Money received less money paid, from the cash book", AlwaysShow: true),
        new(AccountChart.ReceivableAccount, "Accounts Receivable (customers)", "প্রাপ্য হিসাব (ক্রেতা)", Current,
            "Credit sales not yet collected", AlwaysShow: true),
        new(AccountChart.AdvancesDepositsAccount, "Advances, Deposits & Prepayments", "অগ্রিম, জামানত ও অগ্রিম পরিশোধ", Current,
            "Prepaid expenses, deposits and supplier advances"),
        new(AccountChart.InventoryAccount, "Inventory (Feed, Medicine & Supplies)", "মজুদ মালামাল", Current,
            "Stock bought or brought in, less stock used, returned or written off — at cost", AlwaysShow: true),
        new(AccountChart.LivestockAccount, "Livestock (at cost)", "গবাদিপশু (ক্রয়মূল্যে)", Current,
            "Animals bought, less the cost of animals sold, slaughtered or dead", AlwaysShow: true),
    ];

    public static readonly IReadOnlyList<LineSpec> LiabilityLayout =
    [
        new(AccountChart.PayableAccount, "Accounts Payable (suppliers)", "প্রদেয় হিসাব (সরবরাহকারী)", Current,
            "Bought on credit and not yet paid", AlwaysShow: true),
        new(AccountChart.LoanPayableAccount, "Loans Payable (principal)", "প্রদেয় ঋণ (আসল)", Current,
            "Loans received less principal repaid (interest is an expense, not debt reduction)", AlwaysShow: true),
        new(AccountChart.ProvisionsAccount, "Provisions & Accrued Expenses", "সঞ্চিতি ও বকেয়া খরচ", Current,
            "Expenses incurred but not yet paid or billed"),
        new(AccountChart.OtherLiabilitiesAccount, "Other Liabilities", "অন্যান্য দায়", Current, null),
        new(CustomerAdvancesLine, "Customer Advances", "ক্রেতার অগ্রিম", Current,
            "Collected from customers beyond what their invoices now come to"),
        new(OverdraftLine, "Bank Overdraft / Cash Deficit", "ব্যাংক ওভারড্রাফট / নগদ ঘাটতি", Current,
            "More recorded as paid than received — check for a missing opening balance or capital receipt"),
    ];

    public static readonly IReadOnlyList<LineSpec> EquityLayout =
    [
        new(AccountChart.InvestorCapitalAccount, "Investor Capital", "বিনিয়োগকারীর মূলধন", CapitalGroup,
            "Capital invested less capital withdrawn", AlwaysShow: true),
        new(AccountChart.OwnerCapitalAccount, "Owner's Capital", "মালিকের মূলধন", CapitalGroup,
            "Owner's cash, stock, animals and assets brought in", AlwaysShow: true),
        new(AccountChart.OwnerDrawingAccount, "Less: Owner's Drawings", "বাদ: মালিকের উত্তোলন", CapitalGroup,
            "Cash taken out by the owner", IsDeduction: true),
        new(AccountChart.RetainedEarningsAccount, "Retained Earnings brought forward", "পূর্ববর্তী বছরের সঞ্চিত মুনাফা", ReservesGroup,
            "Profit and loss of earlier financial years", AlwaysShow: true),
        new(AccountChart.CurrentYearProfitLine, "Profit / (Loss) for the year", "চলতি বছরের মুনাফা / (ক্ষতি)", ReservesGroup,
            "Net profit of the current financial year to date", AlwaysShow: true),
        new(AccountChart.ProfitDistributionAccount, "Less: Profit Distributed to Investors", "বাদ: বণ্টিত লভ্যাংশ", ReservesGroup,
            "Profit already paid out", IsDeduction: true),
    ];

    private readonly Dictionary<string, decimal> _amounts;

    private BalanceSheetPosition(Dictionary<string, decimal> amounts)
    {
        _amounts = amounts;
    }

    public decimal TotalAssets { get; private init; }
    public decimal TotalLiabilitiesAndEquity { get; private init; }
    public decimal CurrentAssets { get; private init; }
    public decimal CurrentLiabilities { get; private init; }
    public decimal CurrentYearProfit { get; private init; }
    public decimal RetainedBroughtForward { get; private init; }

    /// <summary>The line's amount as presented: positive on its natural side, negative for deductions.</summary>
    public decimal Amount(string code) => _amounts.GetValueOrDefault(code);

    public static BalanceSheetPosition Build(FarmLedger ledger, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        var balances = ledger.BalancesAt(asOf);
        decimal Bal(string code) => balances.GetValueOrDefault(code);

        var fyStart = FiscalYear.StartOf(asOf, ledger.FiscalYearStartMonth);
        var first = ledger.FirstCalculatedDay;
        var yearFrom = fyStart > first ? fyStart : first;
        var currentYearProfit = ledger.ProfitBetween(yearFrom, asOf);
        // Opening retained earnings (credit) plus every profit before this financial year.
        var broughtForward = -Bal(AccountChart.RetainedEarningsAccount)
            + (yearFrom > DateOnly.MinValue ? ledger.ProfitBetween(DateOnly.MinValue, yearFrom.AddDays(-1)) : 0m);

        var cash = Bal(AccountChart.CashAccount);
        var receivable = Bal(AccountChart.ReceivableAccount);
        var payable = -Bal(AccountChart.PayableAccount);

        var a = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            // Assets: debit balances.
            [AccountChart.FixedAssetAccount] = Bal(AccountChart.FixedAssetAccount),
            [AccountChart.AccumulatedDepreciationAccount] = Bal(AccountChart.AccumulatedDepreciationAccount),
            [AccountChart.CashAccount] = Math.Max(0m, cash),
            [AccountChart.ReceivableAccount] = Math.Max(0m, receivable),
            // A supplier paid beyond their bills is an advance to recover, not a negative liability.
            [AccountChart.AdvancesDepositsAccount] = Bal(AccountChart.AdvancesDepositsAccount) + Math.Max(0m, -payable),
            [AccountChart.InventoryAccount] = Bal(AccountChart.InventoryAccount),
            [AccountChart.LivestockAccount] = Bal(AccountChart.LivestockAccount),

            // Liabilities: credit balances.
            [AccountChart.PayableAccount] = Math.Max(0m, payable),
            [AccountChart.LoanPayableAccount] = -Bal(AccountChart.LoanPayableAccount),
            [AccountChart.ProvisionsAccount] = -Bal(AccountChart.ProvisionsAccount),
            [AccountChart.OtherLiabilitiesAccount] = -Bal(AccountChart.OtherLiabilitiesAccount),
            [CustomerAdvancesLine] = Math.Max(0m, -receivable),
            [OverdraftLine] = Math.Max(0m, -cash),

            // Equity: credit balances; drawings and distributions are debits shown as deductions.
            [AccountChart.InvestorCapitalAccount] = -Bal(AccountChart.InvestorCapitalAccount),
            [AccountChart.OwnerCapitalAccount] = -Bal(AccountChart.OwnerCapitalAccount),
            [AccountChart.OwnerDrawingAccount] = -Bal(AccountChart.OwnerDrawingAccount),
            [AccountChart.RetainedEarningsAccount] = broughtForward,
            [AccountChart.CurrentYearProfitLine] = currentYearProfit,
            [AccountChart.ProfitDistributionAccount] = -Bal(AccountChart.ProfitDistributionAccount),
        };

        decimal Total(IEnumerable<LineSpec> layout) => layout.Sum(s => a.GetValueOrDefault(s.Code));

        return new BalanceSheetPosition(a)
        {
            TotalAssets = Total(AssetLayout),
            TotalLiabilitiesAndEquity = Total(LiabilityLayout) + Total(EquityLayout),
            CurrentAssets = Total(AssetLayout.Where(s => s.Group == Current)),
            CurrentLiabilities = Total(LiabilityLayout.Where(s => s.Group == Current)),
            CurrentYearProfit = currentYearProfit,
            RetainedBroughtForward = broughtForward,
        };
    }
}
