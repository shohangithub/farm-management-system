using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetMonthlyPnLReportQuery(Guid FarmId, int Year, int Month) : IRequest<MonthlyPnLReportDto>;

public class GetMonthlyPnLReportQueryHandler : IRequestHandler<GetMonthlyPnLReportQuery, MonthlyPnLReportDto>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetMonthlyPnLReportQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<MonthlyPnLReportDto> Handle(GetMonthlyPnLReportQuery request, CancellationToken cancellationToken)
    {
        var allTransactions = await _repository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        
        var monthTransactions = allTransactions
            .Where(t => t.TransactionDate.Year == request.Year && t.TransactionDate.Month == request.Month)
            .ToList();

        // Profit & loss: revenue and expense accounts only. Capital, loans and stock/animal purchases
        // are balance-sheet movements; the cost of stock and animals reaches P&L when they are used,
        // sold or lost.
        var incomeTransactions = monthTransactions.Where(AccountChart.IsRevenue).ToList();
        var expenseTransactions = monthTransactions.Where(AccountChart.IsExpense).ToList();

        var totalIncome = AccountChart.Revenue(incomeTransactions);
        var totalExpense = AccountChart.Expenses(expenseTransactions);

        var incomeByCategory = incomeTransactions
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(AccountChart.ProfitEffect), 2));

        var expenseByCategory = expenseTransactions
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => Math.Round(-g.Sum(AccountChart.ProfitEffect), 2));

        return new MonthlyPnLReportDto(
            request.FarmId,
            request.Year,
            request.Month,
            Math.Round(totalIncome, 2),
            Math.Round(totalExpense, 2),
            Math.Round(totalIncome - totalExpense, 2),
            incomeByCategory,
            expenseByCategory
        );
    }
}
