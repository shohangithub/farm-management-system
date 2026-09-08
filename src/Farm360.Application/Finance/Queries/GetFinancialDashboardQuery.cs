using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetFinancialDashboardQuery(Guid FarmId) : IRequest<FinancialDashboardDto>;

public class GetFinancialDashboardQueryHandler : IRequestHandler<GetFinancialDashboardQuery, FinancialDashboardDto>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetFinancialDashboardQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<FinancialDashboardDto> Handle(GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
        var allTransactions = await _repository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);
        var currentYearStart = new DateTime(now.Year, 1, 1);

        var currentMonthTx = allTransactions
            .Where(t => t.TransactionDate >= currentMonthStart && t.TransactionDate <= now)
            .ToList();
            
        var previousMonthTx = allTransactions
            .Where(t => t.TransactionDate >= previousMonthStart && t.TransactionDate <= previousMonthEnd)
            .ToList();

        var ytdTx = allTransactions
            .Where(t => t.TransactionDate >= currentYearStart && t.TransactionDate <= now)
            .ToList();

        // MTD Metrics
        var revenueMtd = currentMonthTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var expensesMtd = currentMonthTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var netProfitMtd = revenueMtd - expensesMtd;

        // MOM Comparison
        var prevRevenue = previousMonthTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var prevExpenses = previousMonthTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var prevNetProfit = prevRevenue - prevExpenses;

        var revenueMom = CalculatePercentageChange(prevRevenue, revenueMtd);
        var expensesMom = CalculatePercentageChange(prevExpenses, expensesMtd);
        var netProfitMom = CalculatePercentageChange(prevNetProfit, netProfitMtd);

        // YTD Metrics
        var revenueYtd = ytdTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var expensesYtd = ytdTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var netProfitYtd = revenueYtd - expensesYtd;

        var profitMarginPercent = revenueMtd > 0 ? Math.Round((netProfitMtd / revenueMtd) * 100m, 1) : 0m;

        // 6-Month Cash Flow Trend
        var cashFlowTrend = new List<MonthlyCashFlowPointDto>();
        for (var i = 5; i >= 0; i--)
        {
            var mStart = currentMonthStart.AddMonths(-i);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var mTx = allTransactions.Where(t => t.TransactionDate >= mStart && t.TransactionDate <= mEnd).ToList();
            var mIncome = mTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
            var mExpense = mTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
            var mNet = mIncome - mExpense;
            cashFlowTrend.Add(new MonthlyCashFlowPointDto(
                mStart.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                Math.Round(mIncome, 2),
                Math.Round(mExpense, 2),
                Math.Round(mNet, 2)
            ));
        }

        // Expense Breakdown by Category (MTD or YTD if MTD is empty)
        var sourceExpenses = expensesMtd > 0 ? currentMonthTx.Where(t => t.Type == TransactionType.Expense).ToList() : ytdTx.Where(t => t.Type == TransactionType.Expense).ToList();
        var totalExpAmount = sourceExpenses.Sum(t => t.AmountBdt);
        var expenseBreakdown = sourceExpenses
            .GroupBy(t => t.Category.ToString())
            .Select(g =>
            {
                var sum = g.Sum(x => x.AmountBdt);
                var pct = totalExpAmount > 0 ? Math.Round((sum / totalExpAmount) * 100m, 1) : 0m;
                return new CategoryExpenseBreakdownDto(g.Key, Math.Round(sum, 2), pct);
            })
            .OrderByDescending(x => x.AmountBdt)
            .ToList();

        // Recent 5 Transactions
        var recentTransactions = allTransactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .Select(t => new FinancialTransactionDto(
                t.Id,
                t.FarmId,
                t.Type.ToString(),
                t.Category.ToString(),
                t.AmountBdt,
                t.TransactionDate,
                t.Description,
                t.ReferenceId,
                t.Notes,
                t.AnimalId,
                t.BatchId,
                t.ShedId,
                t.CreatedAtUtc
            ))
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
        );
    }

    private static decimal CalculatePercentageChange(decimal previous, decimal current)
    {
        if (previous == 0)
            return current > 0 ? 100m : 0m;

        var change = ((current - previous) / Math.Abs(previous)) * 100m;
        return Math.Round(change, 2);
    }
}
