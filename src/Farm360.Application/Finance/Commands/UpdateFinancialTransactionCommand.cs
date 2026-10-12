using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public sealed record UpdateFinancialTransactionCommand(
    Guid Id,
    Guid FarmId,
    TransactionCategory Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string Notes,
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null
) : IRequest<FinancialTransactionDto>, ITransactionalCommand;

public sealed class UpdateFinancialTransactionCommandValidator : AbstractValidator<UpdateFinancialTransactionCommand>
{
    public UpdateFinancialTransactionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Transaction ID is required.");
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("Farm ID is required.");
        RuleFor(x => x.AmountBdt).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

public sealed class UpdateFinancialTransactionCommandHandler : IRequestHandler<UpdateFinancialTransactionCommand, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IOverheadAllocationService _overheadAllocation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Farm360.Domain.Finance.Interfaces.ITradeBillRepository _bills;

    public UpdateFinancialTransactionCommandHandler(
        IFinancialTransactionRepository repository,
        IOverheadAllocationService overheadAllocation,
        IUnitOfWork unitOfWork,
        Farm360.Domain.Finance.Interfaces.ITradeBillRepository bills)
    {
        _repository = repository;
        _overheadAllocation = overheadAllocation;
        _unitOfWork = unitOfWork;
        _bills = bills;
    }

    public async Task<FinancialTransactionDto> Handle(UpdateFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

        AccountChart.EnsureEditable(transaction);
        AccountChart.EnsureManualAllowed(transaction.Type, request.Category);

        // On credit: the bill must keep matching the entry it was raised for.
        var bill = transaction.SourceDocumentId is { } sourceId
            ? await _bills.FindBySourceAsync(transaction.FarmId, sourceId, transaction.Category, cancellationToken)
            : null;
        if (bill is not null)
        {
            if (request.Category != transaction.Category)
                throw new InvalidOperationException("This entry is on credit. Cancel its bill in Payables & Receivables before changing the category.");
            if (request.AmountBdt < bill.PaidBdt + bill.AdjustedBdt - TradeBill.Tolerance)
                throw new InvalidOperationException(
                    $"{bill.PaidBdt + bill.AdjustedBdt:N2} BDT has already been settled on this bill, so the amount cannot be lower than that. Void a payment first.");
            if (DateOnly.FromDateTime(request.TransactionDate) > (bill.DueDate ?? DateOnly.MaxValue))
                throw new InvalidOperationException("The date cannot be after the bill's due date. Change the due date first.");
        }

        transaction.UpdateDetails(
            request.Category,
            request.AmountBdt,
            request.TransactionDate,
            request.Description,
            request.Notes,
            request.AnimalId,
            request.BatchId,
            request.ShedId
        );

        await _repository.UpdateAsync(transaction, cancellationToken);
        if (bill is not null)
        {
            bill.Revise(transaction.AmountBdt, DateOnly.FromDateTime(transaction.TransactionDate));
            _bills.Update(bill);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // If this was a shared expense already split onto animals, re-split it from the new
        // amount/date/category (or remove the split if it's no longer shared).
        await _overheadAllocation.ReallocateTransactionAsync(transaction.Id, cancellationToken);

        return transaction.ToDto();
    }
}
