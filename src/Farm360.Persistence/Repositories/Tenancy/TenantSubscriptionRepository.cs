using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Tenancy;

public sealed class TenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public TenantSubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TenantSubscriptionRecord record, CancellationToken cancellationToken = default)
    {
        await _context.TenantSubscriptions.AddAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSubscriptionRecord>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantSubscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantSubscriptionRecord?> GetLatestByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantSubscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
