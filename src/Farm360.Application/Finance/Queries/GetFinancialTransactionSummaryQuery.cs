using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetFinancialTransactionSummaryQuery(Guid FarmId) : IRequest<FinancialTransactionSummaryDto>;

public class GetFinancialTransactionSummaryQueryHandler : IRequestHandler<GetFinancialTransactionSummaryQuery, FinancialTransactionSummaryDto>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetFinancialTransactionSummaryQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<FinancialTransactionSummaryDto> Handle(GetFinancialTransactionSummaryQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);

        var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
        var totalExpense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
        var netBalance = totalIncome - totalExpense;

        return new FinancialTransactionSummaryDto(totalIncome, totalExpense, netBalance);
    }
}
