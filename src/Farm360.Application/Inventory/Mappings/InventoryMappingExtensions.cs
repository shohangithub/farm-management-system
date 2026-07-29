using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Inventory.Mappings;

public static class InventoryMappingExtensions
{
    public static InventoryItemDto ToDto(this InventoryItem item)
    {
        var status = item.CurrentStock == 0 ? InventoryStatus.OutOfStock :
                     item.CurrentStock <= item.ReorderThreshold ? InventoryStatus.LowStock :
                     item.CurrentStock > item.ReorderThreshold * 3 ? InventoryStatus.Excess : InventoryStatus.Sufficient;

        return new InventoryItemDto(
            item.Id,
            item.FarmId,
            item.Name,
            item.Sku,
            item.Category,
            item.Category.ToString(),
            item.UnitOfMeasure,
            item.ReorderThreshold,
            item.CurrentStock,
            item.WeightedAverageCostBdt,
            item.TotalValueBdt,
            status,
            status.ToString(),
            item.StorageLocation,
            item.IsActive);
    }

    public static SupplierDto ToDto(this Supplier supplier)
    {
        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Notes,
            supplier.IsActive);
    }

    public static StockTransactionDto ToDto(this StockTransaction transaction, string itemName, string? supplierName = null)
    {
        return new StockTransactionDto(
            transaction.Id,
            transaction.FarmId,
            transaction.InventoryItemId,
            itemName,
            transaction.TransactionType,
            transaction.TransactionType.ToString(),
            transaction.Quantity,
            transaction.UnitCostBdt,
            transaction.TotalCostBdt,
            transaction.BalanceAfter,
            transaction.TransactionDate,
            transaction.SupplierId,
            supplierName,
            transaction.InvoiceNumber,
            transaction.BatchNumber,
            transaction.ExpiryDate,
            transaction.Reason,
            transaction.RecordedBy);
    }
}
