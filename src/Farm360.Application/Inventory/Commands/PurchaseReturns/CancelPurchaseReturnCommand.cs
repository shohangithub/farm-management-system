using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseReturns;

public sealed record CancelPurchaseReturnCommand(Guid Id, string Reason) : IRequest, ITransactionalCommand;

public sealed class CancelPurchaseReturnCommandValidator : AbstractValidator<CancelPurchaseReturnCommand>
{
    public CancelPurchaseReturnCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Purchase return ID is required.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Cancellation reason is required.");
    }
}

public sealed class CancelPurchaseReturnCommandHandler : IRequestHandler<CancelPurchaseReturnCommand>
{
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchaseReturnCommandHandler(
        IPurchaseReturnRepository returnRepository,
        IUnitOfWork unitOfWork)
    {
        _returnRepository = returnRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelPurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await _returnRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("PurchaseReturn", request.Id);

        purchaseReturn.Cancel(request.Reason);
        await _returnRepository.UpdateAsync(purchaseReturn, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
