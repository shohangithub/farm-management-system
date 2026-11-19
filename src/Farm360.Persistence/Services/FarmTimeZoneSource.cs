using Farm360.Application.Common.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Farm360.Persistence.Services;

/// <summary>
/// Reads the stored zone ids for <see cref="IFarmClock"/>. Runs in its own short-lived scope with
/// query filters off, so it works from requests and from background jobs alike (which have no
/// request tenant), and never interleaves with the caller's DbContext. Results are cached by
/// <see cref="Farm360.Application.Common.Time.FarmTimeZoneCache"/>, so this hits the database at
/// most once per tenant / farm every few minutes.
/// </summary>
public sealed class FarmTimeZoneSource(IServiceScopeFactory scopeFactory, ILogger<FarmTimeZoneSource> logger) : IFarmTimeZoneSource
{
    public IReadOnlyList<string?> GetZoneCandidates(Guid tenantId, Guid? farmId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var candidates = new List<string?>(3);

            if (farmId is { } id && id != Guid.Empty)
            {
                var farmZone = (from f in db.Farms.IgnoreQueryFilters().AsNoTracking()
                                join b in db.Branches.IgnoreQueryFilters() on f.BranchId equals b.Id
                                join o in db.Organizations.IgnoreQueryFilters() on b.OrganizationId equals o.Id
                                where f.Id == id
                                select o.TimeZoneId).FirstOrDefault();
                candidates.Add(farmZone);

                if (tenantId == Guid.Empty)
                {
                    tenantId = db.Farms.IgnoreQueryFilters().AsNoTracking()
                        .Where(f => f.Id == id).Select(f => f.TenantId).FirstOrDefault();
                }
            }

            if (tenantId != Guid.Empty)
            {
                var organizationZone = db.Organizations.IgnoreQueryFilters().AsNoTracking()
                    .Where(o => o.TenantId == tenantId && !o.IsDeleted)
                    .OrderBy(o => o.CreatedAtUtc)
                    .Select(o => o.TimeZoneId)
                    .FirstOrDefault();
                candidates.Add(organizationZone);

                var tenantZone = db.Tenants.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.TimeZone)
                    .FirstOrDefault();
                candidates.Add(tenantZone);
            }

            return candidates;
        }
#pragma warning disable CA1031 // A clock must never fail a request: fall back to the default zone.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Could not read the time zone for tenant {TenantId} / farm {FarmId}; using the default zone.", tenantId, farmId);
            return [];
        }
    }
}
