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

/// <param name="Quantity">In <paramref name="UnitId"/> (e.g. 10 "Bag (50 kg)"), or the base unit when none.</param>
/// <param name="UnitCostBdt">Price of one <paramref name="UnitId"/>.</param>
public record PurchaseOrderItemDto(
    Guid InventoryItemId,
    decimal Quantity,
    decimal UnitCostBdt,
    Guid? UnitId = null);

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryItemRepository? _items;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        IInventoryItemRepository? items = null,
        Farm360.Application.Inventory.Units.IUnitService? units = null)
    {
        _items = items;
        _units = units;
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
            if (item.UnitId is { } unitId && _items is not null && _units is not null)
            {
                // Ordered in packs: the order line is kept in the base unit so receiving and returns stay simple.
                var stockItem = await _items.GetByIdAsync(item.InventoryItemId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Inventory item '{item.InventoryItemId}' was not found.");
                var entered = await _units.ToBaseAsync(stockItem, unitId, item.Quantity, item.UnitCostBdt, cancellationToken);
                purchaseOrder.AddItem(item.InventoryItemId, entered.BaseQuantity, entered.BaseUnitCostBdt, entered.EnteredUnitName, entered.EnteredQuantity);
            }
            else
            {
                purchaseOrder.AddItem(item.InventoryItemId, item.Quantity, item.UnitCostBdt);
            }
        }

        await _repository.AddAsync(purchaseOrder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return purchaseOrder.Id;
    }
}
