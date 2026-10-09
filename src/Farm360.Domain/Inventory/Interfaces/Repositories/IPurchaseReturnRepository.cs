using Farm360.Domain.Inventory.Enums;

namespace Farm360.Domain.Inventory.Interfaces.Repositories;

public interface IPurchaseReturnRepository
{
    Task<PurchaseReturn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseReturn?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PurchaseReturn> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId,
        Guid? purchaseOrderId,
        Guid? supplierId,
        PurchaseReturnStatus? status,
        string? search,
        string? sortBy,
        bool sortDesc,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesByPoAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
