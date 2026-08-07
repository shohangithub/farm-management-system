using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Inventory;

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseOrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseOrders
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
    }

    public Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default)
    {
        _context.PurchaseOrders.Update(purchaseOrder);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId,
        Guid? supplierId,
        PurchaseOrderStatus? status,
        string? search,
        string? sortBy,
        bool sortDesc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseOrders
            .Include(x => x.Items)
            .AsNoTracking();

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (supplierId.HasValue)
            query = query.Where(x => x.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.PoNumber.Contains(search));
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "ponumber" => sortDesc ? query.OrderByDescending(x => x.PoNumber) : query.OrderBy(x => x.PoNumber),
            "orderdate" => sortDesc ? query.OrderByDescending(x => x.OrderDate) : query.OrderBy(x => x.OrderDate),
            _ => query.OrderByDescending(x => x.OrderDate) // Default sort
        };

        var count = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, count);
    }
}
