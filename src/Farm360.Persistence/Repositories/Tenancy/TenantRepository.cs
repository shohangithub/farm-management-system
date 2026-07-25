using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using Farm360.Persistence.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Tenancy;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;

    public TenantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(entity, cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FindAsync([id], cancellationToken);
    }
}
