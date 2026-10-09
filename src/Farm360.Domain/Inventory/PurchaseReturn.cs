using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class PurchaseReturn : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string ReturnNumber { get; private set; } = null!;
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateOnly ReturnDate { get; private set; }
    public PurchaseReturnStatus Status { get; private set; }
    public PurchaseReturnReason Reason { get; private set; }
    public string? Notes { get; private set; }
    public string? CreditNoteNumber { get; private set; }

    private readonly List<PurchaseReturnItem> _items = new();
    public IReadOnlyCollection<PurchaseReturnItem> Items => _items.AsReadOnly();

    public decimal TotalAmountBdt => _items.Sum(i => i.TotalCostBdt);

    private PurchaseReturn() { }

    public PurchaseReturn(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid purchaseOrderId,
        Guid supplierId,
        DateOnly returnDate,
        PurchaseReturnReason reason,
        string? creditNoteNumber = null,
        string? notes = null)
        : base(id, tenantId)
    {
        FarmId = farmId;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        ReturnDate = returnDate;
        Reason = reason;
        CreditNoteNumber = creditNoteNumber?.Trim();
        Notes = notes?.Trim();
        Status = PurchaseReturnStatus.Draft;
        ReturnNumber = GenerateReturnNumber();
    }

    public void AddItem(Guid purchaseOrderItemId, Guid inventoryItemId, decimal quantity, decimal unitCostBdt)
    {
        if (Status != PurchaseReturnStatus.Draft)
            throw new InventoryDomainException($"Cannot add items to a purchase return in '{Status}' status.");

        var existingItem = _items.FirstOrDefault(i => i.PurchaseOrderItemId == purchaseOrderItemId);
        if (existingItem != null)
            throw new InventoryDomainException("Item already exists in this purchase return. Update the quantity instead.");

        _items.Add(new PurchaseReturnItem(Guid.NewGuid(), Id, purchaseOrderItemId, inventoryItemId, quantity, unitCostBdt));
    }

    public void RemoveItem(Guid purchaseReturnItemId)
    {
        if (Status != PurchaseReturnStatus.Draft)
            throw new InventoryDomainException($"Cannot remove items from a purchase return in '{Status}' status.");

        var item = _items.FirstOrDefault(i => i.Id == purchaseReturnItemId)
            ?? throw new InventoryDomainException("Purchase return item not found.");

        _items.Remove(item);
    }

    public void Complete()
    {
        if (Status != PurchaseReturnStatus.Draft)
            throw new InventoryDomainException($"Cannot complete a purchase return in '{Status}' status.");

        if (_items.Count == 0)
            throw new InventoryDomainException("Cannot complete an empty purchase return.");

        Status = PurchaseReturnStatus.Completed;

        RaiseDomainEvent(new PurchaseReturnCompletedEvent(
            Id,
            TenantId,
            FarmId,
            PurchaseOrderId,
            SupplierId,
            ReturnNumber,
            TotalAmountBdt,
            ReturnDate));
    }

    public void Cancel(string reason)
    {
        if (Status != PurchaseReturnStatus.Draft)
            throw new InventoryDomainException($"Cannot cancel a purchase return in '{Status}' status.");

        Status = PurchaseReturnStatus.Cancelled;
        Notes = (string.IsNullOrWhiteSpace(Notes) ? "" : Notes + " | ") + $"Cancelled: {reason.Trim()}";
    }

    private static string GenerateReturnNumber()
    {
        return $"PR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }
}
