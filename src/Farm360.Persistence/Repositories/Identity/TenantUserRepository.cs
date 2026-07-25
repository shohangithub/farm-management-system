using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Persistence.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Identity;

public sealed class TenantUserRepository : ITenantUserRepository
{
    private readonly ApplicationDbContext _context;

    public TenantUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TenantUser entity, CancellationToken cancellationToken = default)
    {
        await _context.TenantUsers.AddAsync(entity, cancellationToken);
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TenantUsers.FindAsync([id], cancellationToken);
    }
}
