using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Dashboard;

/// <summary>
/// Metric queries shared by the executive dashboard and the analytics widgets, so the same KPI
/// cannot be computed two different ways on two cards.
/// </summary>
internal static class DashboardMetricQueries
{
    /// <summary>Animals present on the farm (Active + Quarantined — see <see cref="AnimalStatusGroups.OnFarm"/>).</summary>
    public static IQueryable<Animal> OnFarmAnimals(ApplicationDbContext context, Guid? tenantId, Guid? farmId)
    {
        var query = context.Animals.AsNoTracking().Where(a => AnimalStatusGroups.OnFarm.Contains(a.Status));
        if (tenantId.HasValue)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        if (farmId.HasValue)
        {
            query = query.Where(a => a.FarmId == farmId.Value);
        }

        return query;
    }

    /// <summary>
    /// Distinct on-farm animals affected by an open (not resolved) disease incident.
    /// Incidents link animals through <c>AffectedAnimalIds</c>; those are de-duplicated across
    /// incidents and limited to animals still on the farm. An incident recorded with only a head
    /// count (no linked animals) cannot be de-duplicated, so its unlinked remainder is added as-is;
    /// the total is capped at the on-farm head count so overlapping incidents can never report
    /// more sick animals than the farm holds.
    /// </summary>
    public static async Task<int> CountSickAnimalsAsync(ApplicationDbContext context, Guid? tenantId, Guid? farmId, CancellationToken cancellationToken)
    {
        var incidentQuery = context.DiseaseIncidents.AsNoTracking().Where(i => i.Status != IncidentStatus.Resolved);
        if (tenantId.HasValue)
        {
            incidentQuery = incidentQuery.Where(i => i.TenantId == tenantId.Value);
        }

        if (farmId.HasValue)
        {
            incidentQuery = incidentQuery.Where(i => i.FarmId == farmId.Value);
        }

        var incidents = await incidentQuery
            .Select(i => new { i.AffectedAnimalCount, i.AffectedAnimalIds })
            .ToListAsync(cancellationToken);

        if (incidents.Count == 0)
        {
            return 0;
        }

        var onFarmIds = (await OnFarmAnimals(context, tenantId, farmId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var linked = new HashSet<Guid>();
        var unlinked = 0;
        foreach (var incident in incidents)
        {
            var ids = incident.AffectedAnimalIds ?? [];
            foreach (var id in ids)
            {
                if (onFarmIds.Contains(id))
                {
                    linked.Add(id);
                }
            }

            unlinked += Math.Max(0, incident.AffectedAnimalCount - ids.Count);
        }

        return Math.Min(onFarmIds.Count, linked.Count + unlinked);
    }

    /// <summary>
    /// Profit-and-loss postings (revenue or expense accounts) in [<paramref name="startUtc"/>,
    /// <paramref name="endExclusiveUtc"/>). Feed the result to <see cref="AccountChart.Revenue"/> /
    /// <see cref="AccountChart.Expenses"/> so figures match the finance dashboard exactly.
    /// </summary>
    public static async Task<List<FinancialTransaction>> LoadProfitAndLossPostingsAsync(
        ApplicationDbContext context,
        Guid? tenantId,
        Guid? farmId,
        DateTime startUtc,
        DateTime endExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var plCategories = AccountChart.RevenueCategories.Concat(AccountChart.ExpenseCategories).ToArray();

        var query = context.FinancialTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= startUtc && t.TransactionDate < endExclusiveUtc && plCategories.Contains(t.Category));
        if (tenantId.HasValue)
        {
            query = query.Where(t => t.TenantId == tenantId.Value);
        }

        if (farmId.HasValue)
        {
            query = query.Where(t => t.FarmId == farmId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>[first of the month containing <paramref name="nowUtc"/>, first of next month).</summary>
    public static (DateTime Start, DateTime EndExclusive) CurrentMonth(DateTime nowUtc)
    {
        var start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }
}
