using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

/// <summary>Moves a Draft purchase order to Pending Approval.</summary>
public record SubmitPurchaseOrderCommand(Guid Id) : IRequest<Unit>, ITransactionalCommand;

public class SubmitPurchaseOrderCommandHandler : IRequestHandler<SubmitPurchaseOrderCommand, Unit>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitPurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(SubmitPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        // Items are needed: an empty order cannot be submitted.
        var purchaseOrder = await _repository.GetByIdWithItemsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        purchaseOrder.SubmitForApproval();

        await _repository.UpdateAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
