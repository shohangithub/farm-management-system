using Farm360.Domain.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Identity.Repositories;

public interface ITenantUserRepository
{
    Task AddAsync(TenantUser entity, CancellationToken cancellationToken = default);
    Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
