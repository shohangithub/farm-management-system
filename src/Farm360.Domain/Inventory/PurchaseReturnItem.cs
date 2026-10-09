using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class PurchaseReturnItem : BaseEntity
{
    public Guid PurchaseReturnId { get; private set; }
    public Guid PurchaseOrderItemId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCostBdt { get; private set; }
    public decimal TotalCostBdt => Math.Round(Quantity * UnitCostBdt, 2);

    private PurchaseReturnItem() { }

    internal PurchaseReturnItem(
        Guid id,
        Guid purchaseReturnId,
        Guid purchaseOrderItemId,
        Guid inventoryItemId,
        decimal quantity,
        decimal unitCostBdt) : base(id)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Purchase return item quantity must be greater than zero.");
        if (unitCostBdt < 0)
            throw new InventoryDomainException("Purchase return item unit cost cannot be negative.");

        PurchaseReturnId = purchaseReturnId;
        PurchaseOrderItemId = purchaseOrderItemId;
        InventoryItemId = inventoryItemId;
        Quantity = quantity;
        UnitCostBdt = unitCostBdt;
    }

    internal void UpdateQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Purchase return item quantity must be greater than zero.");

        Quantity = quantity;
    }
}
