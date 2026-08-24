using System;
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

        var currentMonthTx = allTransactions
            .Where(t => t.TransactionDate >= currentMonthStart && t.TransactionDate <= now)
            .ToList();
            
        var previousMonthTx = allTransactions
            .Where(t => t.TransactionDate >= previousMonthStart && t.TransactionDate <= previousMonthEnd)
            .ToList();

        var revenueMtd = currentMonthTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var expensesMtd = currentMonthTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var netProfitMtd = revenueMtd - expensesMtd;

        var prevRevenue = previousMonthTx.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var prevExpenses = previousMonthTx.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var prevNetProfit = prevRevenue - prevExpenses;

        var revenueMom = CalculatePercentageChange(prevRevenue, revenueMtd);
        var expensesMom = CalculatePercentageChange(prevExpenses, expensesMtd);
        var netProfitMom = CalculatePercentageChange(prevNetProfit, netProfitMtd);

        return new FinancialDashboardDto(
            request.FarmId,
            Math.Round(revenueMtd, 2),
            Math.Round(expensesMtd, 2),
            Math.Round(netProfitMtd, 2),
            revenueMom,
            expensesMom,
            netProfitMom
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
