using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public sealed record GetFinancialTransactionByIdQuery(Guid Id, Guid FarmId) : IRequest<FinancialTransactionDto>;

public sealed class GetFinancialTransactionByIdQueryHandler : IRequestHandler<GetFinancialTransactionByIdQuery, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetFinancialTransactionByIdQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<FinancialTransactionDto> Handle(GetFinancialTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

        return new FinancialTransactionDto(
            transaction.Id,
            transaction.FarmId,
            transaction.Type.ToString(),
            transaction.Category.ToString(),
            transaction.AmountBdt,
            transaction.TransactionDate,
            transaction.Description,
            transaction.ReferenceId,
            transaction.Notes,
            transaction.AnimalId,
            transaction.BatchId,
            transaction.ShedId,
            transaction.CreatedAtUtc,
            transaction.IsAutomated,
            transaction.SourceModule
        );
    }
}
