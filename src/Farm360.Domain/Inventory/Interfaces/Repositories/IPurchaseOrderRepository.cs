using Farm360.Domain.Inventory.Enums;

namespace Farm360.Domain.Inventory.Interfaces.Repositories;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);
    
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId,
        Guid? supplierId,
        PurchaseOrderStatus? status,
        string? search,
        string? sortBy,
        bool sortDesc,
        CancellationToken cancellationToken = default);
}
