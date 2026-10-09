using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Tenancy.Repositories;

public interface ITenantSubscriptionRepository
{
    Task AddAsync(TenantSubscriptionRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSubscriptionRecord>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSubscriptionRecord?> GetLatestByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSubscriptionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantSubscriptionRecord?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSubscriptionRecord>> GetPendingAsync(CancellationToken cancellationToken = default);
    void Update(TenantSubscriptionRecord record);
}
