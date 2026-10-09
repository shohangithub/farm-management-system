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
    private readonly IPublisher _publisher;

    public FulfillPurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<Unit> Handle(FulfillPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        // Fulfilling the PO triggers the PurchaseOrderFulfilledEvent, 
        // which will be handled by an event handler to increase stock and post expense.
        purchaseOrder.Fulfill();

        var domainEvents = purchaseOrder.DomainEvents.OfType<Farm360.Domain.Inventory.Events.PurchaseOrderFulfilledEvent>().ToList();

        await _repository.UpdateAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(new Farm360.Application.Inventory.EventHandlers.PurchaseOrderFulfilledNotification(domainEvent), cancellationToken);
        }
        
        return Unit.Value;
    }
}
