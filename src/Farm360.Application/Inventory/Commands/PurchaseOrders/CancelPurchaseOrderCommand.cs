using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

/// <summary>Cancels a Draft, Pending Approval or Approved purchase order. Fulfilled orders cannot be cancelled.</summary>
public record CancelPurchaseOrderCommand(Guid Id, string? Reason) : IRequest<Unit>, ITransactionalCommand;

public class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Unit>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "No reason given" : request.Reason.Trim();
        purchaseOrder.Cancel(reason);

        await _repository.UpdateAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        // Notes column is 1000 chars and the reason is appended to existing notes.
        RuleFor(v => v.Reason).MaximumLength(300);
    }
}
