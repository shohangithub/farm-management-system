using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Inventory;

public sealed class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseReturnRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseReturn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PurchaseReturn?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default)
    {
        await _context.PurchaseReturns.AddAsync(purchaseReturn, cancellationToken);
    }

    public Task UpdateAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default)
    {
        _context.PurchaseReturns.Update(purchaseReturn);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<PurchaseReturn> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId,
        Guid? purchaseOrderId,
        Guid? supplierId,
        PurchaseReturnStatus? status,
        string? search,
        string? sortBy,
        bool sortDesc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseReturns
            .Include(x => x.Items)
            .AsNoTracking();

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (purchaseOrderId.HasValue)
            query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);

        if (supplierId.HasValue)
            query = query.Where(x => x.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.ReturnNumber.Contains(search) || (x.CreditNoteNumber != null && x.CreditNoteNumber.Contains(search)));
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "returnnumber" => sortDesc ? query.OrderByDescending(x => x.ReturnNumber) : query.OrderBy(x => x.ReturnNumber),
            "returndate" => sortDesc ? query.OrderByDescending(x => x.ReturnDate) : query.OrderBy(x => x.ReturnDate),
            "status" => sortDesc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => query.OrderByDescending(x => x.ReturnDate)
        };

        var count = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, count);
    }

    public async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesByPoAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseReturns
            .Where(pr => pr.PurchaseOrderId == purchaseOrderId && pr.Status == PurchaseReturnStatus.Completed)
            .SelectMany(pr => pr.Items)
            .GroupBy(item => item.PurchaseOrderItemId)
            .Select(g => new { PurchaseOrderItemId = g.Key, TotalReturned = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.PurchaseOrderItemId, x => x.TotalReturned, cancellationToken);
    }
}
