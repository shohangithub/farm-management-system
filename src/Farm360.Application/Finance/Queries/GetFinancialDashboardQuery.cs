using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetFinancialDashboardQuery(Guid FarmId) : IRequest<FinancialDashboardDto>;

/// <remarks>
/// Profit figures come from <see cref="IFinancialStatementEngine"/>, so they include depreciation,
/// disposals and year-end adjustments and agree with the income statement. "YTD" is the farm's
/// fiscal year to date (the year the balance sheet reports "Profit for the year" over).
/// </remarks>
public class GetFinancialDashboardQueryHandler : IRequestHandler<GetFinancialDashboardQuery, FinancialDashboardDto>
{
    private readonly IFinancialStatementEngine _engine;

    public GetFinancialDashboardQueryHandler(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public async Task<FinancialDashboardDto> Handle(GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ledger = await _engine.LoadAsync(request.FarmId, today, cancellationToken);

        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);
        var fiscalYearStart = FiscalYear.StartOf(today, ledger.FiscalYearStartMonth);

        // MTD Metrics — profit & loss only. Investor capital, loans and stock/animal purchases move
        // cash but are not revenue or expense; counting them made a capital injection look like profit.
        var revenueMtd = ledger.RevenueBetween(currentMonthStart, today);
        var expensesMtd = ledger.ExpensesBetween(currentMonthStart, today);
        var netProfitMtd = revenueMtd - expensesMtd;

        // MOM Comparison (the whole previous month, up to and including its last day)
        var prevRevenue = ledger.RevenueBetween(previousMonthStart, previousMonthEnd);
        var prevExpenses = ledger.ExpensesBetween(previousMonthStart, previousMonthEnd);
        var prevNetProfit = prevRevenue - prevExpenses;

        var revenueMom = CalculatePercentageChange(prevRevenue, revenueMtd);
        var expensesMom = CalculatePercentageChange(prevExpenses, expensesMtd);
        var netProfitMom = CalculatePercentageChange(prevNetProfit, netProfitMtd);

        // Fiscal YTD Metrics
        var revenueYtd = ledger.RevenueBetween(fiscalYearStart, today);
        var expensesYtd = ledger.ExpensesBetween(fiscalYearStart, today);
        var netProfitYtd = revenueYtd - expensesYtd;

        var profitMarginPercent = revenueMtd > 0 ? Math.Round((netProfitMtd / revenueMtd) * 100m, 1) : 0m;

        // 6-Month Cash Flow Trend — real cash only (non-cash postings such as stock consumed are
        // excluded); each month runs from its first day up to the next month's first day.
        var cashFlowTrend = new List<MonthlyCashFlowPointDto>();
        for (var i = 5; i >= 0; i--)
        {
            var mStart = currentMonthStart.AddMonths(-i);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var mTx = ledger.PostingsBetween(mStart, mEnd);
            var mIncome = AccountChart.CashIn(mTx);
            var mExpense = AccountChart.CashOut(mTx);
            var mNet = mIncome - mExpense;
            cashFlowTrend.Add(new MonthlyCashFlowPointDto(
                mStart.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                Math.Round(mIncome, 2),
                Math.Round(mExpense, 2),
                Math.Round(mNet, 2)
            ));
        }

        // Expense Breakdown by head (MTD, or fiscal YTD if MTD is empty) — depreciation and
        // disposals included, so the heads add up to the expense total.
        var breakdownFrom = expensesMtd > 0 ? currentMonthStart : fiscalYearStart;
        var heads = ledger.ProfitAndLossLinesBetween(breakdownFrom, today)
            .Where(l => !FarmLedger.IsRevenue(l.Account))
            .GroupBy(l => AccountChart.CategoryKey(l.Account))
            .Select(g => (Key: g.Key, Sum: g.Sum(l => l.Amount)))
            .ToList();
        // Percent of the heads that add to the total, so a gain on disposal (negative) cannot push others above 100%.
        var percentBase = heads.Where(h => h.Sum > 0m).Sum(h => h.Sum);
        var expenseBreakdown = heads
            .Select(h =>
            {
                var pct = percentBase > 0 ? Math.Round((h.Sum / percentBase) * 100m, 1) : 0m;
                return new CategoryExpenseBreakdownDto(h.Key, Math.Round(h.Sum, 2), pct);
            })
            .Where(x => x.AmountBdt != 0m)
            .OrderByDescending(x => x.AmountBdt)
            .ToList();

        // Recent 5 Transactions
        var recentTransactions = ledger.AllPostings
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .Select(t => t.ToDto())
            .ToList();

        return new FinancialDashboardDto(
            request.FarmId,
            Math.Round(revenueMtd, 2),
            Math.Round(expensesMtd, 2),
            Math.Round(netProfitMtd, 2),
            revenueMom,
            expensesMom,
            netProfitMom,
            Math.Round(revenueYtd, 2),
            Math.Round(expensesYtd, 2),
            Math.Round(netProfitYtd, 2),
            profitMarginPercent,
            cashFlowTrend,
            expenseBreakdown,
            recentTransactions
        )
        {
            FiscalYearStart = fiscalYearStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            FiscalYearStartMonth = ledger.FiscalYearStartMonth,
        };
    }

    private static decimal CalculatePercentageChange(decimal previous, decimal current)
    {
        if (previous == 0)
            return current > 0 ? 100m : 0m;

        var change = ((current - previous) / Math.Abs(previous)) * 100m;
        return Math.Round(change, 2);
    }
}
