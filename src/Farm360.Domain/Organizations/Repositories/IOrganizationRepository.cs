using Farm360.Domain.Common;

namespace Farm360.Domain.Organizations.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Organization> Items, int TotalCount)> GetPagedByTenantAsync(
        Guid tenantId, 
        string? searchTerm, 
        int? status, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the organization with this name in the tenant, or null if not found.
    /// Used for uniqueness-excluding-self checks on update.
    /// </summary>
    Task<Organization?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
    void Add(Organization organization);
    void Update(Organization organization);
}

