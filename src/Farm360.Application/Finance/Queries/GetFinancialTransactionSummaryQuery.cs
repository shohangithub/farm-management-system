using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
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

        // Cash view: money actually received and paid. Non-cash postings (stock consumed, cost of
        // animals sold) are real costs but moved no cash, so they do not belong in a cash balance.
        var totalIncome = AccountChart.CashIn(transactions);
        var totalExpense = AccountChart.CashOut(transactions);
        var netBalance = totalIncome - totalExpense;

        return new FinancialTransactionSummaryDto(totalIncome, totalExpense, netBalance);
    }
}
