using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetFinancialTransactionsQuery(Guid FarmId) : IRequest<IReadOnlyList<FinancialTransactionDto>>;

public class GetFinancialTransactionsQueryHandler : IRequestHandler<GetFinancialTransactionsQuery, IReadOnlyList<FinancialTransactionDto>>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetFinancialTransactionsQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FinancialTransactionDto>> Handle(GetFinancialTransactionsQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);

        return transactions.Select(t => new FinancialTransactionDto(
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
        )).ToList();
    }
}
