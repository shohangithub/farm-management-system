using Farm360.Domain.Inventory.Enums;

namespace Farm360.Domain.Inventory.Interfaces.Repositories;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryItem>> GetByFarmIdAsync(Guid farmId, InventoryCategory? category = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InventoryItem> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        InventoryCategory? category = null,
        InventoryStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);
    void Update(InventoryItem item);
    void Delete(InventoryItem item);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    void Update(Supplier supplier);
}

public interface IStockTransactionRepository
{
    Task<StockTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockTransaction>> GetByItemIdAsync(Guid inventoryItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockTransaction>> GetByFarmIdAsync(Guid farmId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<(StockTransaction Transaction, string ItemName, string? SupplierName)> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? inventoryItemId = null,
        StockTransactionType? transactionType = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);
    Task AddAsync(StockTransaction transaction, CancellationToken cancellationToken = default);
}
