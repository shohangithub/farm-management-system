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

    public async Task<TenantSubscriptionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<TenantSubscriptionRecord?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken cancellationToken = default)
    {
        return await _context.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.PaymentReference == paymentReference, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSubscriptionRecord>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TenantSubscriptions
            .Where(s => s.Status == "Pending")
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Update(TenantSubscriptionRecord record)
    {
        _context.TenantSubscriptions.Update(record);
    }
}
