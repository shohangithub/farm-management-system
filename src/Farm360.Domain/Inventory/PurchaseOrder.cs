using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class PurchaseOrder : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string PoNumber { get; private set; } = null!;
    public Guid SupplierId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDeliveryDate { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    public decimal TotalAmountBdt => _items.Sum(i => i.TotalCostBdt);

    private PurchaseOrder() { }

    public PurchaseOrder(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid supplierId,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate = null,
        string? notes = null)
        : base(id, tenantId)
    {
        FarmId = farmId;
        SupplierId = supplierId;
        Status = PurchaseOrderStatus.Draft;
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        Notes = notes?.Trim();
        PoNumber = GeneratePoNumber();
    }

    public void AddItem(Guid inventoryItemId, decimal quantity, decimal unitCostBdt)
    {
        if (Status != PurchaseOrderStatus.Draft && Status != PurchaseOrderStatus.PendingApproval)
            throw new InventoryDomainException($"Cannot add items to a purchase order in '{Status}' status.");

        var existingItem = _items.FirstOrDefault(i => i.InventoryItemId == inventoryItemId);
        if (existingItem != null)
        {
            throw new InventoryDomainException("Item already exists in the purchase order. Update the quantity instead.");
        }

        _items.Add(new PurchaseOrderItem(Guid.NewGuid(), Id, inventoryItemId, quantity, unitCostBdt));
    }

    public void RemoveItem(Guid purchaseOrderItemId)
    {
        if (Status != PurchaseOrderStatus.Draft && Status != PurchaseOrderStatus.PendingApproval)
            throw new InventoryDomainException($"Cannot remove items from a purchase order in '{Status}' status.");

        var item = _items.FirstOrDefault(i => i.Id == purchaseOrderItemId)
            ?? throw new InventoryDomainException("Purchase order item not found.");

        _items.Remove(item);
    }

    public void SubmitForApproval()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InventoryDomainException("Only draft purchase orders can be submitted for approval.");
        if (_items.Count == 0)
            throw new InventoryDomainException("Cannot submit an empty purchase order.");

        Status = PurchaseOrderStatus.PendingApproval;
    }

    public void Approve(string approvedBy)
    {
        if (Status != PurchaseOrderStatus.PendingApproval)
            throw new InventoryDomainException("Only pending purchase orders can be approved.");

        Status = PurchaseOrderStatus.Approved;
    }

    public void Cancel(string reason)
    {
        if (Status == PurchaseOrderStatus.Fulfilled)
            throw new InventoryDomainException("Cannot cancel a fulfilled purchase order.");

        Status = PurchaseOrderStatus.Cancelled;
        Notes = (string.IsNullOrWhiteSpace(Notes) ? "" : Notes + " | ") + $"Cancelled: {reason}";
    }

    public void Fulfill()
    {
        if (Status != PurchaseOrderStatus.Approved)
            throw new InventoryDomainException("Only approved purchase orders can be fulfilled.");

        Status = PurchaseOrderStatus.Fulfilled;

        // Domain event triggers stock-in for all items
        RaiseDomainEvent(new PurchaseOrderFulfilledEvent(Id, TenantId, FarmId));
    }

    private static string GeneratePoNumber()
    {
        return $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }
}
