using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record RecordExpenseCommand(
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
    Guid? ShedId,
    CreditTermsRequest? Credit = null
) : IRequest<FinancialTransactionDto>;

public class RecordExpenseCommandHandler : IRequestHandler<RecordExpenseCommand, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITradeCreditService _credit;

    public RecordExpenseCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork, ITradeCreditService credit)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _credit = credit;
    }

    public async Task<FinancialTransactionDto> Handle(RecordExpenseCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionCategory>(request.Category, true, out var category))
            throw new ArgumentException($"Invalid transaction category: {request.Category}");

        AccountChart.EnsureManualAllowed(TransactionType.Expense, category);

        var transaction = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Expense,
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

        // Not paid on the day: the supplier is owed (Accounts Payable) until settled.
        if (request.Credit is { } credit)
        {
            await _credit.OpenAsync(transaction, new CreditTerms(
                credit.PartyName, credit.PartyPhone, credit.DueDate, credit.PaidNowBdt, credit.PaymentMethod, credit.Notes),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return transaction.ToDto();
    }
}
