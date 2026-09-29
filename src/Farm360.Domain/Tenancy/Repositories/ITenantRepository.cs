using Farm360.Domain.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Tenancy.Repositories;

public interface ITenantRepository
{
    Task AddAsync(Tenant entity, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Update(Tenant entity);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(int UsersCount, int FarmsCount, int AnimalsCount)> GetTenantUsageCountsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
