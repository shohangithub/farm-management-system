using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Finance;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetMonthlyPnLReportQuery(Guid FarmId, int Year, int Month) : IRequest<MonthlyPnLReportDto>;

/// <summary>
/// Profit and loss for one calendar month, from the same <see cref="IFinancialStatementEngine"/>
/// lines as the income statement and balance sheet — so depreciation, disposals and year-end
/// adjustments are included and the twelve months of a financial year add up to its net profit.
/// </summary>
public class GetMonthlyPnLReportQueryHandler : IRequestHandler<GetMonthlyPnLReportQuery, MonthlyPnLReportDto>
{
    private readonly IFinancialStatementEngine _engine;

    public GetMonthlyPnLReportQueryHandler(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public async Task<MonthlyPnLReportDto> Handle(GetMonthlyPnLReportQuery request, CancellationToken cancellationToken)
    {
        var from = new DateOnly(request.Year, request.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var ledger = await _engine.LoadAsync(request.FarmId, to, cancellationToken);
        var movements = ledger.MovementsBetween(from, to)
            .Where(m => FarmLedger.IsProfitAndLoss(m.Key) && m.Value != 0m)
            .ToList();

        // Revenue accounts move on the credit side, expenses on the debit side.
        var incomeByCategory = movements
            .Where(m => AccountChart.Account(m.Key).Class == AccountClass.Revenue)
            .GroupBy(m => AccountChart.CategoryKey(m.Key))
            .ToDictionary(g => g.Key, g => Math.Round(-g.Sum(m => m.Value), 2));

        var expenseByCategory = movements
            .Where(m => AccountChart.Account(m.Key).Class == AccountClass.Expense)
            .GroupBy(m => AccountChart.CategoryKey(m.Key))
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(m => m.Value), 2));

        var totalIncome = incomeByCategory.Values.Sum();
        var totalExpense = expenseByCategory.Values.Sum();

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
