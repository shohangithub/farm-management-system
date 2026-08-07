using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCostBdt { get; private set; }
    public decimal TotalCostBdt => Math.Round(Quantity * UnitCostBdt, 2);

    private PurchaseOrderItem() { }

    internal PurchaseOrderItem(Guid id, Guid purchaseOrderId, Guid inventoryItemId, decimal quantity, decimal unitCostBdt) : base(id)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Purchase order item quantity must be greater than zero.");
        if (unitCostBdt < 0)
            throw new InventoryDomainException("Purchase order item unit cost cannot be negative.");

        PurchaseOrderId = purchaseOrderId;
        InventoryItemId = inventoryItemId;
        Quantity = quantity;
        UnitCostBdt = unitCostBdt;
    }

    internal void UpdateDetails(decimal quantity, decimal unitCostBdt)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Purchase order item quantity must be greater than zero.");
        if (unitCostBdt < 0)
            throw new InventoryDomainException("Purchase order item unit cost cannot be negative.");

        Quantity = quantity;
        UnitCostBdt = unitCostBdt;
    }
}
