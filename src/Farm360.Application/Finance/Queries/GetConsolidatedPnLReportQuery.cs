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

public record GetConsolidatedPnLReportQuery(Guid TenantId, int Year, int Month) : IRequest<ConsolidatedPnLReportDto>;

public class GetConsolidatedPnLReportQueryHandler : IRequestHandler<GetConsolidatedPnLReportQuery, ConsolidatedPnLReportDto>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetConsolidatedPnLReportQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ConsolidatedPnLReportDto> Handle(GetConsolidatedPnLReportQuery request, CancellationToken cancellationToken)
    {
        var allTransactions = await _repository.GetAllByTenantIdAsync(request.TenantId, cancellationToken);
        
        var monthTransactions = allTransactions
            .Where(t => t.TransactionDate.Year == request.Year && t.TransactionDate.Month == request.Month)
            .ToList();

        // Profit & loss only: capital, loans and asset purchases are not income or expense.
        var totalIncome = AccountChart.Revenue(monthTransactions);
        var totalExpense = AccountChart.Expenses(monthTransactions);

        var farmBreakdown = monthTransactions
            .GroupBy(t => t.FarmId)
            .ToDictionary(
                g => g.Key,
                g => new FarmPnLSnapshotDto(
                    g.Key,
                    Math.Round(AccountChart.Revenue(g), 2),
                    Math.Round(AccountChart.Expenses(g), 2),
                    Math.Round(AccountChart.NetProfit(g), 2)
                )
            );

        return new ConsolidatedPnLReportDto(
            request.Year,
            request.Month,
            Math.Round(totalIncome, 2),
            Math.Round(totalExpense, 2),
            Math.Round(totalIncome - totalExpense, 2),
            farmBreakdown
        );
    }
}
