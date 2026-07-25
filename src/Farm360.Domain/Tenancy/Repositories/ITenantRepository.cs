using Farm360.Domain.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Tenancy.Repositories;

public interface ITenantRepository
{
    Task AddAsync(Tenant entity, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
