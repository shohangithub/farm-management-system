using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
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
) : IRequest<FinancialTransactionDto>;

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
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFinancialTransactionCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FinancialTransactionDto> Handle(UpdateFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

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
