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

        var incomeTransactions = monthTransactions.Where(t => t.Type == TransactionType.Income).ToList();
        var expenseTransactions = monthTransactions.Where(t => t.Type == TransactionType.Expense).ToList();

        var totalIncome = incomeTransactions.Sum(t => t.AmountBdt);
        var totalExpense = expenseTransactions.Sum(t => t.AmountBdt);

        var incomeByCategory = incomeTransactions
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(t => t.AmountBdt));

        var expenseByCategory = expenseTransactions
            .GroupBy(t => t.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(t => t.AmountBdt));

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
