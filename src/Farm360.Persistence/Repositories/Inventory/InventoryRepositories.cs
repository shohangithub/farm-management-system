using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Inventory;

public sealed class InventoryItemRepository : IInventoryItemRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public InventoryItemRepository(ApplicationDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenantService.TenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByFarmIdAsync(Guid farmId, InventoryCategory? category = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems.Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId);

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        return await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<InventoryItem> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        InventoryCategory? category = null,
        InventoryStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems
            .AsNoTracking()
            .Where(x => x.TenantId == _tenantService.TenantId);

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (category.HasValue)
            query = query.Where(x => x.Category == category.Value);

        if (status.HasValue)
        {
            query = status.Value switch
            {
                InventoryStatus.OutOfStock => query.Where(x => x.CurrentStock == 0),
                InventoryStatus.LowStock => query.Where(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderThreshold),
                InventoryStatus.Excess => query.Where(x => x.CurrentStock > x.ReorderThreshold * 3),
                InventoryStatus.Sufficient => query.Where(x => x.CurrentStock > x.ReorderThreshold && x.CurrentStock <= x.ReorderThreshold * 3),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%") || EF.Functions.Like(x.Sku, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("name", false)             => query.OrderBy(x => x.Name),
            ("name", true)              => query.OrderByDescending(x => x.Name),
            ("sku", false)              => query.OrderBy(x => x.Sku),
            ("sku", true)               => query.OrderByDescending(x => x.Sku),
            ("currentstock", false)     => query.OrderBy(x => x.CurrentStock),
            ("currentstock", true)      => query.OrderByDescending(x => x.CurrentStock),
            ("weightedaveragecostbdt", false) or ("cost", false) => query.OrderBy(x => x.WeightedAverageCostBdt),
            ("weightedaveragecostbdt", true) or ("cost", true)   => query.OrderByDescending(x => x.WeightedAverageCostBdt),
            ("createdat", false)        => query.OrderBy(x => x.CreatedAtUtc),
            _                           => query.OrderByDescending(x => x.CreatedAtUtc)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), totalCount);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId && x.CurrentStock <= x.ReorderThreshold)
            .OrderBy(x => x.CurrentStock)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        await _dbContext.InventoryItems.AddAsync(item, cancellationToken);
    }

    public void Update(InventoryItem item)
    {
        _dbContext.InventoryItems.Update(item);
    }

    public void Delete(InventoryItem item)
    {
        _dbContext.InventoryItems.Remove(item);
    }
}

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public SupplierRepository(ApplicationDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Suppliers
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenantService.TenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Suppliers
            .Where(x => x.TenantId == _tenantService.TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.TenantId == _tenantService.TenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%") ||
                                     (x.ContactPerson != null && EF.Functions.Like(x.ContactPerson, $"%{term}%")) ||
                                     (x.Phone != null && EF.Functions.Like(x.Phone, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("name", false)       => query.OrderBy(x => x.Name),
            ("name", true)        => query.OrderByDescending(x => x.Name),
            ("createdat", false)  => query.OrderBy(x => x.CreatedAtUtc),
            _                     => query.OrderByDescending(x => x.CreatedAtUtc)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), totalCount);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public void Update(Supplier supplier)
    {
        _dbContext.Suppliers.Update(supplier);
    }

    public void Delete(Supplier supplier)
    {
        _dbContext.Suppliers.Remove(supplier);
    }
}

public sealed class StockTransactionRepository : IStockTransactionRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public StockTransactionRepository(ApplicationDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<StockTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StockTransactions
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenantService.TenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<StockTransaction>> GetByItemIdAsync(Guid inventoryItemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StockTransactions
            .Where(x => x.InventoryItemId == inventoryItemId && x.TenantId == _tenantService.TenantId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockTransaction>> GetByFarmIdAsync(Guid farmId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.StockTransactions.Where(x => x.FarmId == farmId && x.TenantId == _tenantService.TenantId);

        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate <= toDate.Value);

        return await query.OrderByDescending(x => x.TransactionDate).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<(StockTransaction Transaction, string ItemName, string? SupplierName)> Items, int TotalCount)> GetPagedAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = from t in _dbContext.StockTransactions.AsNoTracking().Where(x => x.TenantId == _tenantService.TenantId)
                    join i in _dbContext.InventoryItems.AsNoTracking() on t.InventoryItemId equals i.Id into itemGroup
                    from item in itemGroup.DefaultIfEmpty()
                    join s in _dbContext.Suppliers.AsNoTracking() on t.SupplierId equals s.Id into supplierGroup
                    from supplier in supplierGroup.DefaultIfEmpty()
                    select new
                    {
                        Transaction = t,
                        ItemName = item != null ? item.Name : "Unknown Item",
                        SupplierName = supplier != null ? supplier.Name : (string?)null
                    };

        if (farmId.HasValue)
            query = query.Where(x => x.Transaction.FarmId == farmId.Value);

        if (inventoryItemId.HasValue)
            query = query.Where(x => x.Transaction.InventoryItemId == inventoryItemId.Value);

        if (transactionType.HasValue)
            query = query.Where(x => x.Transaction.TransactionType == transactionType.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.Transaction.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.Transaction.TransactionDate <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x => EF.Functions.Like(x.ItemName, $"%{term}%") ||
                                     (x.SupplierName != null && EF.Functions.Like(x.SupplierName, $"%{term}%")) ||
                                     (x.Transaction.Reason != null && EF.Functions.Like(x.Transaction.Reason, $"%{term}%")) ||
                                     (x.Transaction.InvoiceNumber != null && EF.Functions.Like(x.Transaction.InvoiceNumber, $"%{term}%")) ||
                                     (x.Transaction.BatchNumber != null && EF.Functions.Like(x.Transaction.BatchNumber, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("itemname", false)         => query.OrderBy(x => x.ItemName),
            ("itemname", true)          => query.OrderByDescending(x => x.ItemName),
            ("suppliername", false)     => query.OrderBy(x => x.SupplierName),
            ("suppliername", true)      => query.OrderByDescending(x => x.SupplierName),
            ("transactiondate", false)  => query.OrderBy(x => x.Transaction.TransactionDate).ThenBy(x => x.Transaction.CreatedAtUtc),
            ("transactiondate", true)   => query.OrderByDescending(x => x.Transaction.TransactionDate).ThenByDescending(x => x.Transaction.CreatedAtUtc),
            ("quantity", false)         => query.OrderBy(x => x.Transaction.Quantity),
            ("quantity", true)          => query.OrderByDescending(x => x.Transaction.Quantity),
            ("totalcostbdt", false)     => query.OrderBy(x => x.Transaction.Quantity * x.Transaction.UnitCostBdt),
            ("totalcostbdt", true)      => query.OrderByDescending(x => x.Transaction.Quantity * x.Transaction.UnitCostBdt),
            ("createdat", false)        => query.OrderBy(x => x.Transaction.CreatedAtUtc),
            _                           => query.OrderByDescending(x => x.Transaction.TransactionDate).ThenByDescending(x => x.Transaction.CreatedAtUtc)
        };

        var pagedList = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var resultItems = pagedList
            .Select(x => ((StockTransaction Transaction, string ItemName, string? SupplierName))(x.Transaction, x.ItemName, x.SupplierName))
            .ToList()
            .AsReadOnly();

        return (resultItems, totalCount);
    }

    public async Task AddAsync(StockTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.StockTransactions.AddAsync(transaction, cancellationToken);
    }
}
