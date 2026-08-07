using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

public record CreatePurchaseOrderCommand(
    Guid FarmId,
    Guid SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    IReadOnlyList<PurchaseOrderItemDto> Items) : IRequest<Guid>, ITransactionalCommand;

public record PurchaseOrderItemDto(
    Guid InventoryItemId,
    decimal Quantity,
    decimal UnitCostBdt);

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = new PurchaseOrder(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.SupplierId,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.Notes);

        foreach (var item in request.Items)
        {
            purchaseOrder.AddItem(item.InventoryItemId, item.Quantity, item.UnitCostBdt);
        }

        await _repository.AddAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return purchaseOrder.Id;
    }
}
