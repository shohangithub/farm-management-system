using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class StockTransaction : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public StockTransactionType TransactionType { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCostBdt { get; private set; }
    public decimal TotalCostBdt => Math.Round(Quantity * UnitCostBdt, 2);
    public decimal BalanceAfter { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public string? BatchNumber { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string? Reason { get; private set; }
    public string? RecordedBy { get; private set; }
    public Guid? ReferenceId { get; private set; }

    private StockTransaction() { }

    public StockTransaction(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid inventoryItemId,
        StockTransactionType transactionType,
        decimal quantity,
        decimal unitCostBdt,
        decimal balanceAfter,
        DateOnly transactionDate,
        Guid? supplierId = null,
        string? invoiceNumber = null,
        string? batchNumber = null,
        DateOnly? expiryDate = null,
        string? reason = null,
        string? recordedBy = null,
        Guid? referenceId = null)
        : base(id, tenantId)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Transaction quantity must be greater than zero.");
        if (unitCostBdt < 0)
            throw new InventoryDomainException("Unit cost cannot be negative.");

        FarmId = farmId;
        InventoryItemId = inventoryItemId;
        TransactionType = transactionType;
        Quantity = quantity;
        UnitCostBdt = unitCostBdt;
        BalanceAfter = balanceAfter;
        TransactionDate = transactionDate;
        SupplierId = supplierId;
        InvoiceNumber = invoiceNumber?.Trim();
        BatchNumber = batchNumber?.Trim();
        ExpiryDate = expiryDate;
        Reason = reason?.Trim();
        RecordedBy = recordedBy?.Trim();
        ReferenceId = referenceId;
    }
}
