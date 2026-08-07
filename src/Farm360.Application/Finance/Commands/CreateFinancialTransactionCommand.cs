using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record CreateFinancialTransactionCommand(
    Guid TenantId,
    Guid FarmId,
    string Type,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string ReferenceId,
    string Notes
) : IRequest<FinancialTransactionDto>;

public class CreateFinancialTransactionCommandHandler : IRequestHandler<CreateFinancialTransactionCommand, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFinancialTransactionCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FinancialTransactionDto> Handle(CreateFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionType>(request.Type, true, out var type))
            throw new ArgumentException($"Invalid transaction type: {request.Type}");

        if (!Enum.TryParse<TransactionCategory>(request.Category, true, out var category))
            throw new ArgumentException($"Invalid transaction category: {request.Category}");

        var transaction = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            type,
            category,
            request.AmountBdt,
            request.TransactionDate,
            request.ReferenceId,
            request.Notes
        );

        await _repository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FinancialTransactionDto(
            transaction.Id,
            transaction.FarmId,
            transaction.Type.ToString(),
            transaction.Category.ToString(),
            transaction.AmountBdt,
            transaction.TransactionDate,
            transaction.ReferenceId,
            transaction.Notes,
            transaction.CreatedAtUtc
        );
    }
}
