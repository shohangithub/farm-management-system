using Farm360.Domain.Common;

namespace Farm360.Domain.Organizations.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
    void Add(Organization organization);
    void Update(Organization organization);
}
