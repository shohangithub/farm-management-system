using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class InventoryItem : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public InventoryCategory Category { get; private set; }
    public string UnitOfMeasure { get; private set; } = null!;
    public decimal ReorderThreshold { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal WeightedAverageCostBdt { get; private set; }
    public decimal TotalValueBdt => CurrentStock * WeightedAverageCostBdt;
    public string? StorageLocation { get; private set; }
    public bool IsActive { get; private set; } = true;

    private InventoryItem() { }

    public InventoryItem(
        Guid id,
        Guid tenantId,
        Guid farmId,
        string name,
        string sku,
        InventoryCategory category,
        string unitOfMeasure,
        decimal reorderThreshold,
        decimal initialStock = 0,
        decimal initialCostBdt = 0,
        string? storageLocation = null)
        : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Inventory item name cannot be empty.");
        if (string.IsNullOrWhiteSpace(unitOfMeasure))
            throw new InventoryDomainException("Unit of measure cannot be empty.");
        if (reorderThreshold < 0)
            throw new InventoryDomainException("Reorder threshold cannot be negative.");
        if (initialStock < 0)
            throw new InventoryDomainException("Initial stock cannot be negative.");

        FarmId = farmId;
        Name = name.Trim();
        Sku = string.IsNullOrWhiteSpace(sku) ? GenerateSku(name) : sku.Trim().ToUpperInvariant();
        Category = category;
        UnitOfMeasure = unitOfMeasure.Trim();
        ReorderThreshold = reorderThreshold;
        CurrentStock = initialStock;
        WeightedAverageCostBdt = initialCostBdt >= 0 ? initialCostBdt : 0;
        StorageLocation = storageLocation?.Trim();
    }

    public void UpdateDetails(string name, InventoryCategory category, string unitOfMeasure, decimal reorderThreshold, string? storageLocation)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Inventory item name cannot be empty.");
        if (string.IsNullOrWhiteSpace(unitOfMeasure))
            throw new InventoryDomainException("Unit of measure cannot be empty.");
        if (reorderThreshold < 0)
            throw new InventoryDomainException("Reorder threshold cannot be negative.");

        Name = name.Trim();
        Category = category;
        UnitOfMeasure = unitOfMeasure.Trim();
        ReorderThreshold = reorderThreshold;
        StorageLocation = storageLocation?.Trim();
    }

    public void ReceiveStock(decimal receivedQuantity, decimal unitCostBdt, Guid transactionId)
    {
        if (receivedQuantity <= 0)
            throw new InventoryDomainException("Received stock quantity must be greater than zero.");
        if (unitCostBdt < 0)
            throw new InventoryDomainException("Unit cost cannot be negative.");

        decimal currentTotalValue = CurrentStock * WeightedAverageCostBdt;
        decimal receivedTotalValue = receivedQuantity * unitCostBdt;
        decimal newStock = CurrentStock + receivedQuantity;

        WeightedAverageCostBdt = newStock > 0 ? Math.Round((currentTotalValue + receivedTotalValue) / newStock, 2) : unitCostBdt;
        CurrentStock = newStock;

        RaiseDomainEvent(new StockReceivedEvent(transactionId, Id, TenantId, FarmId, receivedQuantity, unitCostBdt, CurrentStock));
    }

    public void DeductStock(decimal quantity, Guid transactionId)
    {
        if (quantity <= 0)
            throw new InventoryDomainException("Deducted stock quantity must be greater than zero.");
        if (quantity > CurrentStock)
            throw new InventoryDomainException($"Insufficient stock for '{Name}'. Requested: {quantity} {UnitOfMeasure}, Available: {CurrentStock} {UnitOfMeasure}.");

        CurrentStock -= quantity;

        RaiseDomainEvent(new StockDeductedEvent(transactionId, Id, TenantId, FarmId, quantity, CurrentStock));

        if (CurrentStock <= ReorderThreshold)
        {
            RaiseDomainEvent(new LowStockAlertEvent(Id, TenantId, FarmId, Name, CurrentStock, ReorderThreshold));
        }
    }

    public void AdjustStock(decimal newQuantity, string reason)
    {
        if (newQuantity < 0)
            throw new InventoryDomainException("Stock quantity cannot be negative.");

        CurrentStock = newQuantity;

        if (CurrentStock <= ReorderThreshold)
        {
            RaiseDomainEvent(new LowStockAlertEvent(Id, TenantId, FarmId, Name, CurrentStock, ReorderThreshold));
        }
    }

    public void WriteOffStock(decimal quantity, string reason, Guid transactionId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InventoryDomainException("Write-off reason must be provided.");

        DeductStock(quantity, transactionId);

        RaiseDomainEvent(new StockWriteOffEvent(
            transactionId, 
            Id, 
            TenantId, 
            FarmId, 
            quantity, 
            reason, 
            WeightedAverageCostBdt));
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }

    private static string GenerateSku(string name)
    {
        string prefix = name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant();
        string suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"{prefix}-{suffix}";
    }
}
