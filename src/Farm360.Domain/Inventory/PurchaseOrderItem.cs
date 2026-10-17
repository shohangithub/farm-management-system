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

    /// <summary>The unit it was ordered in ("Bag (50 kg)") when not the base unit; Quantity is always base.</summary>
    public string? EnteredUnitName { get; private set; }
    public decimal? EnteredQuantity { get; private set; }

    internal void SetEnteredUnit(string? unitName, decimal? quantity)
    {
        EnteredUnitName = string.IsNullOrWhiteSpace(unitName) ? null : unitName.Trim();
        EnteredQuantity = EnteredUnitName is null ? null : quantity;
    }

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
