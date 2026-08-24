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

        var totalIncome = monthTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var totalExpense = monthTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);

        var farmBreakdown = monthTransactions
            .GroupBy(t => t.FarmId)
            .ToDictionary(
                g => g.Key,
                g => new FarmPnLSnapshotDto(
                    g.Key,
                    g.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt),
                    g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt),
                    g.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt) - g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt)
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
