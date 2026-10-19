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
    Task<(int TotalItems, decimal TotalValueBdt, int LowStockCount, int OutOfStockCount)> GetSummaryAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);
    void Update(InventoryItem item);
    void Delete(InventoryItem item);

    /// <summary>The tenant's items not yet linked to a base unit (created before units existed).</summary>
    Task<IReadOnlyList<InventoryItem>> GetWithoutBaseUnitAsync(CancellationToken cancellationToken = default);

    /// <summary>The tenant's items counted in a base unit.</summary>
    Task<IReadOnlyList<InventoryItem>> GetByBaseUnitAsync(Guid measureUnitId, CancellationToken cancellationToken = default);
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
    void Delete(Supplier supplier);
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

public interface IMeasureUnitRepository
{
    /// <summary>Every base unit of the tenant with its conversions.</summary>
    Task<IReadOnlyList<MeasureUnit>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MeasureUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>The base unit a conversion belongs to, with all its conversions.</summary>
    Task<MeasureUnit?> GetByConversionIdAsync(Guid conversionId, CancellationToken cancellationToken = default);
    Task AddAsync(MeasureUnit unit, CancellationToken cancellationToken = default);
    void Update(MeasureUnit unit);
    /// <summary>Soft-deletes a base unit and its conversions.</summary>
    void Remove(MeasureUnit unit);
    Task<UnitConversion?> GetConversionAsync(Guid id, CancellationToken cancellationToken = default);
    void RemoveConversion(UnitConversion conversion);
    /// <summary>Items whose base unit or default unit is one of these (to stop deleting units in use).</summary>
    Task<IReadOnlyList<string>> ItemsUsingAsync(IReadOnlyCollection<Guid> baseUnitIds, IReadOnlyCollection<Guid> conversionIds, CancellationToken cancellationToken = default);
}
