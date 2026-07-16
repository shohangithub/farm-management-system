namespace Farm360.Domain.Organizations.Repositories;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<Branch?> GetByCodeAsync(Guid tenantId, string branchCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> GetAllByOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string branchCode, CancellationToken cancellationToken = default);
    
    void Add(Branch branch);
    void Update(Branch branch);
    void Delete(Branch branch);
}
