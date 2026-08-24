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

public record RecordIncomeCommand(
    Guid TenantId,
    Guid FarmId,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string ReferenceId,
    string Notes,
    Guid? AnimalId,
    Guid? BatchId,
    Guid? ShedId
) : IRequest<FinancialTransactionDto>;

public class RecordIncomeCommandHandler : IRequestHandler<RecordIncomeCommand, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordIncomeCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FinancialTransactionDto> Handle(RecordIncomeCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionCategory>(request.Category, true, out var category))
            throw new ArgumentException($"Invalid transaction category: {request.Category}");

        var transaction = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Income,
            category,
            request.AmountBdt,
            request.TransactionDate,
            request.ReferenceId,
            request.Notes,
            request.Description,
            request.AnimalId,
            request.BatchId,
            request.ShedId
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
            transaction.Description,
            transaction.ReferenceId,
            transaction.Notes,
            transaction.AnimalId,
            transaction.BatchId,
            transaction.ShedId,
            transaction.CreatedAtUtc
        );
    }
}
