using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <summary>
/// The complete accounts statement for a period — everything money-related the farm recorded:
/// cash position, profit &amp; loss, cost analysis (labour, feed, health…), stock and livestock
/// movements, investment &amp; financing, investor capital, profit sharing, share capital, loans,
/// a trend and the full cash book.
/// </summary>
public record GetFullAccountsReportQuery(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate
) : IRequest<FullAccountsReportDto>;

public class GetFullAccountsReportQueryHandler : IRequestHandler<GetFullAccountsReportQuery, FullAccountsReportDto>
{
    /// <summary>Ranges up to this many days trend by day; longer ones trend by month.</summary>
    private const int MaxDailyTrendDays = 62;

    private static readonly (string Key, string Name, string NameBn)[] CostGroups =
    [
        (AccountChart.CostLivestock, "Cost of Animals Sold & Mortality", "বিক্রিত/মৃত পশুর ক্রয়মূল্য"),
        (AccountChart.CostFeed, "Feed & Nutrition", "খাদ্য"),
        (AccountChart.CostHealth, "Health & Medicine", "চিকিৎসা ও ঔষধ"),
        (AccountChart.CostLabour, "Labour & Wages", "শ্রমিক মজুরি"),
        (AccountChart.CostInventory, "Consumables & Stock Losses", "ভোগ্যপণ্য ও মজুদ ক্ষতি"),
        (AccountChart.CostOverhead, "Utilities, Transport & Overhead", "ইউটিলিটি, পরিবহন ও অন্যান্য"),
        (AccountChart.CostFinance, "Loan Interest", "ঋণের সুদ"),
    ];

    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IInvestorRepository _investorRepository;
    private readonly IFarmShareRepository _shareRepository;
    private readonly ILoanRecordRepository _loanRepository;
    private readonly ISender _sender;

    public GetFullAccountsReportQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        IInvestorRepository investorRepository,
        IFarmShareRepository shareRepository,
        ILoanRecordRepository loanRepository,
        ISender sender)
    {
        _transactionRepository = transactionRepository;
        _investorRepository = investorRepository;
        _shareRepository = shareRepository;
        _loanRepository = loanRepository;
        _sender = sender;
    }

    public async Task<FullAccountsReportDto> Handle(GetFullAccountsReportQuery request, CancellationToken cancellationToken)
    {
        var (start, endExclusive) = AccountChart.Window(request.FromDate, request.ToDate);
        var end = endExclusive.AddDays(-1);
        var days = (int)(endExclusive - start).TotalDays;

        var allTx = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var before = allTx.Where(t => t.TransactionDate < start).ToList();
        var inPeriod = allTx
            .Where(t => t.TransactionDate >= start && t.TransactionDate < endExclusive)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.CreatedAtUtc)
            .ToList();
        var cashRows = inPeriod.Where(t => t.AffectsCash).ToList();

        // ── Cash position (cash book only) ───────────────────────────────────
        var openingBalance = before.Sum(AccountChart.CashEffect);
        var totalInflow = AccountChart.CashIn(cashRows);
        var totalOutflow = AccountChart.CashOut(cashRows);
        var cash = new CashPositionDto(
            R(openingBalance),
            R(totalInflow),
            R(totalOutflow),
            R(totalInflow - totalOutflow),
            R(openingBalance + totalInflow - totalOutflow),
            cashRows.Count);

        // ── Profit & loss (revenue and expense accounts, cash or not) ────────
        var revenueRows = inPeriod.Where(AccountChart.IsRevenue).ToList();
        var expenseRows = inPeriod.Where(AccountChart.IsExpense).ToList();
        var revenue = AccountChart.Revenue(revenueRows);
        var opCost = AccountChart.Expenses(expenseRows);
        var netOperating = revenue - opCost;
        var operating = new OperatingStatementDto(
            GetIncomeExpenseReportQueryHandler.SummariseHeads(revenueRows, revenue),
            GetIncomeExpenseReportQueryHandler.SummariseHeads(expenseRows, opCost),
            R(revenue),
            R(opCost),
            R(netOperating),
            revenue > 0 ? R(netOperating / revenue * 100m) : 0m);

        var costs = BuildCostAnalysis(expenseRows, opCost, days);
        var capital = BuildCapitalFlow(cashRows);
        var assets = BuildAssetMovement(before, inPeriod);

        // ── Investors, profit sharing, shares, loans ─────────────────────────
        var investors = await _investorRepository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);
        var shareConfig = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken);
        var holdings = await _shareRepository.GetHoldingsByFarmIdAsync(request.FarmId, cancellationToken);
        var shareTx = await _shareRepository.GetTransactionsByFarmIdAsync(request.FarmId, cancellationToken);
        var loans = await _loanRepository.GetByFarmIdAsync(request.FarmId, cancellationToken);

        // Profit entitlement reuses the Investor P&L query so this report and the Profit Sharing
        // page can never disagree about who is owed what for the same period.
        var pnl = await _sender.Send(
            new GetInvestorPnLQuery(request.FarmId, start, endExclusive.AddTicks(-1)),
            cancellationToken);

        var investorAccounts = BuildInvestorAccounts(investors, holdings, shareConfig, pnl, start, endExclusive);
        var distributedInPeriod = investorAccounts.Sum(i => i.ProfitPaidInPeriodBdt);
        var totalEntitled = investorAccounts.Sum(i => i.ProfitEntitledInPeriodBdt);
        var profitShare = new ProfitShareSummaryDto(
            R(pnl.NetFarmProfitBdt),
            pnl.CharityPercentage,
            R(pnl.CharityAmountBdt),
            R(pnl.DistributableProfitBdt),
            R(totalEntitled),
            R(distributedInPeriod),
            R(pnl.DistributableProfitBdt - totalEntitled));

        var investorNames = investors.ToDictionary(i => i.Id, i => i.Name);
        var shares = BuildShareSummary(shareConfig, shareTx, investorNames, start, endExclusive);
        var loanSummary = BuildLoanSummary(loans, inPeriod, start, endExclusive);

        var granularity = days <= MaxDailyTrendDays ? "Daily" : "Monthly";
        var trend = BuildTrend(inPeriod, start, endExclusive, openingBalance, granularity == "Daily");
        var ledger = BuildLedger(cashRows, openingBalance);

        return new FullAccountsReportDto(
            request.FarmId,
            start,
            end,
            days,
            granularity,
            cash,
            operating,
            costs,
            capital,
            assets,
            investorAccounts,
            profitShare,
            shares,
            loanSummary,
            trend,
            ledger)
        {
            Credit = BuildCreditMovement(before, inPeriod),
        };
    }

    private static TradeCreditMovementDto BuildCreditMovement(List<FinancialTransaction> before, List<FinancialTransaction> inPeriod)
    {
        var opening = AccountChart.Balances(before);
        var closing = AccountChart.Balances(before.Concat(inPeriod));

        decimal Settled(SettlementAccount settlement, TransactionType type) =>
            inPeriod.Where(t => t.Settlement == settlement && t.Type == type).Sum(t => t.AmountBdt);
        decimal Category(TransactionCategory category) =>
            inPeriod.Where(t => t.Category == category).Sum(t => t.AmountBdt);

        return new TradeCreditMovementDto(
            R(opening.GetValueOrDefault(AccountChart.ReceivableAccount)),
            R(Settled(SettlementAccount.Receivable, TransactionType.Income)),
            R(Category(TransactionCategory.CustomerReceipt)),
            R(Settled(SettlementAccount.Receivable, TransactionType.Expense)),
            R(closing.GetValueOrDefault(AccountChart.ReceivableAccount)),
            R(-opening.GetValueOrDefault(AccountChart.PayableAccount)),
            R(Settled(SettlementAccount.Payable, TransactionType.Expense)),
            R(Category(TransactionCategory.SupplierPayment)),
            R(Settled(SettlementAccount.Payable, TransactionType.Income)),
            R(-closing.GetValueOrDefault(AccountChart.PayableAccount)));
    }

    private static decimal R(decimal value) => Math.Round(value, 2);

    private static CostAnalysisDto BuildCostAnalysis(List<FinancialTransaction> expenseRows, decimal total, int days)
    {
        var byGroup = expenseRows.ToLookup(t => AccountChart.For(t.Category).CostGroup ?? AccountChart.CostOverhead);

        var groups = CostGroups
            .Select(g =>
            {
                var items = byGroup[g.Key].ToList();
                var amount = -items.Sum(AccountChart.ProfitEffect);
                return new CostGroupDto(
                    g.Key,
                    g.Name,
                    g.NameBn,
                    R(amount),
                    total > 0 ? R(amount / total * 100m) : 0m,
                    items.Count);
            })
            .ToList();

        decimal Of(string key) => groups.First(g => g.Key == key).AmountBdt;

        return new CostAnalysisDto(
            groups,
            R(total),
            Of(AccountChart.CostLabour),
            Of(AccountChart.CostFeed),
            Of(AccountChart.CostHealth),
            Of(AccountChart.CostLivestock),
            Of(AccountChart.CostOverhead) + Of(AccountChart.CostInventory),
            R(-expenseRows.Where(t => t.IsAutomated).Sum(AccountChart.ProfitEffect)),
            R(-expenseRows.Where(t => !t.IsAutomated).Sum(AccountChart.ProfitEffect)),
            days > 0 ? R(total / days) : 0m);
    }

    private static CapitalFlowDto BuildCapitalFlow(List<FinancialTransaction> cashRows)
    {
        decimal Sum(TransactionCategory category) =>
            cashRows.Where(t => t.Category == category).Sum(t => t.AmountBdt);

        var capitalIn = Sum(TransactionCategory.InvestorCapital);
        var withdrawn = Sum(TransactionCategory.InvestorWithdrawal);
        var distributed = Sum(TransactionCategory.ProfitDistribution);
        var loanIn = Sum(TransactionCategory.LoanDisbursement);
        var loanOut = Sum(TransactionCategory.LoanRepayment);
        var ownerIn = Sum(TransactionCategory.OwnerCapital);
        var drawings = Sum(TransactionCategory.OwnerDrawing);

        return new CapitalFlowDto(
            R(capitalIn),
            R(withdrawn),
            R(distributed),
            R(loanIn),
            R(loanOut),
            R(capitalIn + ownerIn + loanIn - withdrawn - distributed - drawings - loanOut))
        {
            OwnerCapitalInBdt = R(ownerIn),
            OwnerDrawingBdt = R(drawings),
            LoanInterestPaidBdt = R(Sum(TransactionCategory.InterestExpense)),
        };
    }

    private static AssetMovementDto BuildAssetMovement(List<FinancialTransaction> before, List<FinancialTransaction> inPeriod)
    {
        var opening = AccountChart.Balances(before);
        var closing = AccountChart.Balances(before.Concat(inPeriod));

        decimal Sum(TransactionCategory category, SettlementAccount? settlement = null) =>
            inPeriod.Where(t => t.Category == category && (settlement is null || t.Settlement == settlement)).Sum(t => t.AmountBdt);

        decimal Consumed(params TransactionCategory[] categories) =>
            inPeriod.Where(t => t.Settlement == SettlementAccount.Inventory && categories.Contains(t.Category) && t.Type == TransactionType.Expense)
                .Sum(t => t.AmountBdt);

        return new AssetMovementDto(
            R(opening.GetValueOrDefault(AccountChart.InventoryAccount)),
            R(Sum(TransactionCategory.InventoryPurchase)),
            R(Sum(TransactionCategory.OwnerCapital, SettlementAccount.Inventory)),
            R(Sum(TransactionCategory.InventoryPurchaseReturn)),
            R(Consumed(TransactionCategory.FeedCost, TransactionCategory.MedicineCost, TransactionCategory.ConsumableExpense,
                TransactionCategory.VeterinaryCost, TransactionCategory.MiscellaneousExpense)),
            R(Consumed(TransactionCategory.InventoryWriteOff)),
            R(closing.GetValueOrDefault(AccountChart.InventoryAccount)),
            R(opening.GetValueOrDefault(AccountChart.LivestockAccount)),
            R(Sum(TransactionCategory.AnimalPurchase)),
            R(Sum(TransactionCategory.OwnerCapital, SettlementAccount.Livestock)),
            R(Sum(TransactionCategory.CostOfAnimalsSold)),
            R(Sum(TransactionCategory.LivestockLoss)),
            R(closing.GetValueOrDefault(AccountChart.LivestockAccount)));
    }

    private static List<InvestorAccountDto> BuildInvestorAccounts(
        IReadOnlyList<Investor> investors,
        IReadOnlyList<ShareHolding> holdings,
        FarmShareConfig? shareConfig,
        InvestorPnLSummaryDto pnl,
        DateTime start,
        DateTime endExclusive)
    {
        var pnlByInvestor = pnl.InvestorShares.ToDictionary(s => s.InvestorId);
        var holdingByInvestor = holdings
            .Where(h => h.ShareCount > 0)
            .GroupBy(h => h.InvestorId)
            .ToDictionary(g => g.Key, g => g.Sum(h => h.ShareCount));

        var result = new List<InvestorAccountDto>();

        foreach (var inv in investors)
        {
            var txs = inv.Transactions.ToList();

            decimal SumOf(InvestorTransactionType type, Func<InvestorTransaction, bool> when) =>
                txs.Where(t => t.Type == type && when(t)).Sum(t => t.AmountBdt);

            bool Before(InvestorTransaction t) => t.TransactionDate < start;
            bool Within(InvestorTransaction t) => t.TransactionDate >= start && t.TransactionDate < endExclusive;
            bool UpToEnd(InvestorTransaction t) => t.TransactionDate < endExclusive;

            var opening = SumOf(InvestorTransactionType.CapitalContribution, Before)
                          - SumOf(InvestorTransactionType.CapitalWithdrawal, Before);
            var contributed = SumOf(InvestorTransactionType.CapitalContribution, Within);
            var withdrawn = SumOf(InvestorTransactionType.CapitalWithdrawal, Within);
            var paidInPeriod = SumOf(InvestorTransactionType.ProfitDistribution, Within);
            var paidLifetime = SumOf(InvestorTransactionType.ProfitDistribution, UpToEnd);

            pnlByInvestor.TryGetValue(inv.Id, out var share);
            var entitled = share?.AllocatedProfitBdt ?? 0m;
            var shareCount = holdingByInvestor.GetValueOrDefault(inv.Id);

            // An investor created after this period has nothing to say about it.
            var hasActivity = opening != 0 || contributed != 0 || withdrawn != 0 || paidInPeriod != 0
                              || entitled != 0 || shareCount > 0 || inv.InvestmentDate < endExclusive;
            if (!hasActivity)
            {
                continue;
            }

            result.Add(new InvestorAccountDto(
                inv.Id,
                inv.Name,
                inv.Phone,
                inv.IsActive,
                inv.AgreedProfitSharePercentage,
                R(opening),
                R(contributed),
                R(withdrawn),
                R(opening + contributed - withdrawn),
                share?.EffectiveSharePercentage ?? 0m,
                R(entitled),
                R(paidInPeriod),
                R(paidLifetime),
                R(entitled - paidInPeriod),
                shareCount,
                shareConfig is { TotalShares: > 0 } ? R((decimal)shareCount / shareConfig.TotalShares * 100m) : 0m,
                shareConfig is null ? 0m : R(shareCount * shareConfig.SharePriceBdt)));
        }

        return result
            .OrderByDescending(i => i.ClosingCapitalBdt)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ShareCapitalSummaryDto BuildShareSummary(
        FarmShareConfig? config,
        IReadOnlyList<ShareTransaction> shareTx,
        Dictionary<Guid, string> investorNames,
        DateTime start,
        DateTime endExclusive)
    {
        var inPeriod = shareTx
            .Where(t => t.TransactionDate >= start && t.TransactionDate < endExclusive)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        var purchases = inPeriod.Where(t => t.Type is ShareTransactionType.Purchase or ShareTransactionType.Bonus).ToList();
        var sales = inPeriod.Where(t => t.Type == ShareTransactionType.Sale).ToList();

        // A transfer is always written as a pair of equal rows (giver and receiver), so halving the
        // sum counts each moved share once.
        var transferred = inPeriod
            .Where(t => t.Type == ShareTransactionType.Transfer)
            .Sum(t => t.ShareCount) / 2;

        var rows = inPeriod
            .Select(t => new ShareTransactionDto(
                t.Id,
                t.FarmId,
                t.InvestorId,
                investorNames.GetValueOrDefault(t.InvestorId, "Unknown Investor"),
                t.Type.ToString(),
                t.ShareCount,
                t.PricePerShareBdt,
                t.TotalAmountBdt,
                t.TransactionDate,
                t.CounterpartyInvestorId,
                t.CounterpartyInvestorId is { } cp ? investorNames.GetValueOrDefault(cp) : null,
                t.ReferenceId,
                t.Notes,
                t.FinancialTransactionId,
                t.CreatedAtUtc))
            .ToList();

        return new ShareCapitalSummaryDto(
            config is not null,
            config?.TotalShares ?? 0,
            config?.SharePriceBdt ?? 0m,
            config?.OwnerShareCount ?? 0,
            config?.AllocatedShareCount ?? 0,
            config?.AvailableShareCount ?? 0,
            config?.TotalValuationBdt ?? 0m,
            config?.OwnerOwnershipPercentage ?? 0m,
            purchases.Sum(t => t.ShareCount),
            R(purchases.Sum(t => t.TotalAmountBdt)),
            sales.Sum(t => t.ShareCount),
            R(sales.Sum(t => t.TotalAmountBdt)),
            transferred,
            rows);
    }

    private static LoanSummaryDto BuildLoanSummary(
        IReadOnlyList<LoanRecord> loans,
        List<FinancialTransaction> inPeriod,
        DateTime start,
        DateTime endExclusive)
    {
        // Repayments carry the lender in their description ("Repayment for loan from X" / "Interest
        // on loan from X"); each is matched to the latest loan from that lender disbursed on or before it.
        LoanRecord? Match(FinancialTransaction tx) => loans
            .Where(l => tx.Description.EndsWith(l.LenderName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(l => l.DisbursementDate <= tx.TransactionDate)
            .ThenByDescending(l => l.DisbursementDate)
            .FirstOrDefault();

        var repaidByLoan = new Dictionary<Guid, decimal>();
        foreach (var tx in inPeriod.Where(t => t.Category is TransactionCategory.LoanRepayment or TransactionCategory.InterestExpense))
        {
            if (Match(tx) is { } loan)
                repaidByLoan[loan.Id] = repaidByLoan.GetValueOrDefault(loan.Id) + tx.AmountBdt;
        }

        var relevant = loans
            .Where(l => l.DisbursementDate < endExclusive)
            .OrderBy(l => l.DisbursementDate)
            .Select(l => new LoanAccountDto(
                l.Id,
                l.LenderName,
                l.DisbursementDate,
                R(l.PrincipalAmountBdt),
                l.InterestRatePercent,
                R(l.TotalPayableBdt),
                R(repaidByLoan.GetValueOrDefault(l.Id)),
                R(l.TotalRepaidBdt),
                R(l.OutstandingBalanceBdt),
                R(l.PrincipalRepaidBdt),
                R(l.InterestPaidBdt),
                R(l.OutstandingPrincipalBdt),
                l.DisbursementDate >= start,
                l.IsActive))
            .ToList();

        return new LoanSummaryDto(
            R(relevant.Where(l => l.DisbursedInPeriod).Sum(l => l.PrincipalAmountBdt)),
            R(inPeriod.Where(t => t.Category == TransactionCategory.LoanRepayment).Sum(t => t.AmountBdt)),
            R(inPeriod.Where(t => t.Category == TransactionCategory.InterestExpense).Sum(t => t.AmountBdt)),
            R(relevant.Sum(l => l.PrincipalAmountBdt)),
            R(relevant.Sum(l => l.OutstandingBdt)),
            relevant.Count(l => l.IsActive),
            relevant);
    }

    private static List<PeriodTrendDto> BuildTrend(
        List<FinancialTransaction> inPeriod,
        DateTime start,
        DateTime endExclusive,
        decimal openingBalance,
        bool daily)
    {
        var result = new List<PeriodTrendDto>();
        var running = openingBalance;
        var bucketStart = start;

        while (bucketStart < endExclusive)
        {
            var bucketEnd = daily
                ? bucketStart.AddDays(1)
                : new DateTime(bucketStart.Year, bucketStart.Month, 1, 0, 0, 0, bucketStart.Kind).AddMonths(1);
            if (bucketEnd > endExclusive)
            {
                bucketEnd = endExclusive;
            }

            var from = bucketStart;
            var to = bucketEnd;
            var txs = inPeriod.Where(t => t.TransactionDate >= from && t.TransactionDate < to).ToList();

            // Operating = profit & loss; capital in/out = cash that moved outside P&L (investors,
            // owner, loans, stock and animal purchases).
            var opIn = AccountChart.Revenue(txs);
            var opOut = AccountChart.Expenses(txs);
            var nonPnlCash = txs.Where(t => t.AffectsCash && !AccountChart.IsProfitAndLoss(t)).ToList();
            var capIn = AccountChart.CashIn(nonPnlCash);
            var capOut = AccountChart.CashOut(nonPnlCash);
            running += txs.Sum(AccountChart.CashEffect);

            // Long ranges skip silent days; months are always shown so the trend has no gaps.
            if (!daily || txs.Count > 0 || (endExclusive - start).TotalDays <= 31)
            {
                result.Add(new PeriodTrendDto(
                    daily
                        ? from.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
                        : from.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    from,
                    to.AddDays(-1),
                    R(opIn),
                    R(opOut),
                    R(opIn - opOut),
                    R(capIn),
                    R(capOut),
                    R(running)));
            }

            bucketStart = bucketEnd;
        }

        return result;
    }

    private static List<LedgerEntryDto> BuildLedger(List<FinancialTransaction> cashRows, decimal openingBalance)
    {
        var running = openingBalance;
        var result = new List<LedgerEntryDto>(cashRows.Count);

        foreach (var t in cashRows)
        {
            running += AccountChart.CashEffect(t);
            var head = AccountChart.For(t.Category);
            result.Add(new LedgerEntryDto(
                t.Id,
                t.TransactionDate,
                AccountChart.VoucherNo(t),
                head.Code,
                head.Name,
                head.Group,
                AccountChart.Describe(t),
                t.Type == TransactionType.Income ? R(t.AmountBdt) : 0m,
                t.Type == TransactionType.Expense ? R(t.AmountBdt) : 0m,
                R(running),
                t.IsAutomated,
                t.SourceModule));
        }

        return result;
    }
}
