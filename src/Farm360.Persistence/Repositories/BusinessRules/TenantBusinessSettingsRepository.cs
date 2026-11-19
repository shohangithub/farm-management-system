using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.BusinessRules;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.BusinessRules;

public sealed class TenantBusinessSettingsRepository(ApplicationDbContext context) : ITenantBusinessSettingsRepository
{
    public Task<TenantBusinessSettings?> GetAsync(Guid? farmId, CancellationToken cancellationToken = default) =>
        context.TenantBusinessSettings.FirstOrDefaultAsync(s => s.FarmId == farmId, cancellationToken);

    // Synchronous on purpose: IFarmBusinessRulesProvider.Current is a property read by dozens of
    // callers. The result is cached per tenant, so this runs once per tenant per cache lifetime.
    public IReadOnlyList<TenantBusinessSettings> GetAllForTenant(Guid tenantId) =>
        context.TenantBusinessSettings
            .IgnoreQueryFilters() // explicit tenant below: jobs resolve tenants other than the ambient one
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .ToList();

    public void Add(TenantBusinessSettings settings) => context.TenantBusinessSettings.Add(settings);
}
