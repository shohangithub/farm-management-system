using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.BusinessRules;

public interface ITenantBusinessSettingsRepository
{
    /// <summary>
    /// The tenant-wide row (<paramref name="farmId"/> null) or one farm's override row, for the
    /// current request's tenant, tracked for update. Null when the tenant has not customised it.
    /// </summary>
    Task<TenantBusinessSettings?> GetAsync(Guid? farmId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live settings row (tenant-wide and per-farm) of <paramref name="tenantId"/>, read without
    /// tracking and independently of the ambient request tenant, so background jobs that work across
    /// tenants can resolve each tenant's rules.
    /// </summary>
    IReadOnlyList<TenantBusinessSettings> GetAllForTenant(Guid tenantId);

    void Add(TenantBusinessSettings settings);
}
