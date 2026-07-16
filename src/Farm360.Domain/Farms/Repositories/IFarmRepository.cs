namespace Farm360.Domain.Farms.Repositories;

public interface IFarmRepository
{
    Task<Farm?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<Farm?> GetByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Farm>> GetAllByBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Farm>> GetAllByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default);
    
    void Add(Farm farm);
    void Update(Farm farm);
    void Delete(Farm farm);
}
