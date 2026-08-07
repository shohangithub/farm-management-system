using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

public record FulfillPurchaseOrderCommand(Guid Id) : IRequest<Unit>, ITransactionalCommand;

public class FulfillPurchaseOrderCommandHandler : IRequestHandler<FulfillPurchaseOrderCommand, Unit>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public FulfillPurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(FulfillPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        // Fulfilling the PO triggers the PurchaseOrderFulfilledEvent, 
        // which will be handled by an event handler to increase stock.
        purchaseOrder.Fulfill();

        await _repository.UpdateAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Unit.Value;
    }
}
