using Farm360.Application.Finance.Services;
using Farm360.Domain.Dashboard.Interfaces;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Intelligence;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Dashboard;

public sealed class ExecutiveDashboardRepository : IExecutiveDashboardRepository
{
    private readonly ApplicationDbContext _context;

    public ExecutiveDashboardRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Animals currently on the farm (Active + Quarantined) — not sold/dead history.</summary>
    public async Task<int> GetTotalAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        return await DashboardMetricQueries.OnFarmAnimals(_context, tenantId, farmId).CountAsync(cancellationToken);
    }

    /// <summary>Distinct on-farm animals in open disease incidents (see <see cref="DashboardMetricQueries.CountSickAnimalsAsync"/>).</summary>
    public Task<int> GetSickAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        return DashboardMetricQueries.CountSickAnimalsAsync(_context, tenantId, farmId, cancellationToken);
    }

    public async Task<int> GetFeedLowStockCountAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryItems.AsNoTracking().Where(i => i.TenantId == tenantId && i.CurrentStock <= i.ReorderThreshold);
        if (farmId.HasValue)
        {
            query = query.Where(i => i.FarmId == farmId.Value);
        }
        return await query.CountAsync(cancellationToken);
    }

    // Revenue / expense are classified by AccountChart (the same code the finance dashboard uses):
    // only P&L accounts count, and income-type rows on expense accounts (refunds, asset-disposal
    // proceeds) reduce expense instead of being dropped or counted as revenue.
    public async Task<decimal> GetCurrentMonthIncomeAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DashboardMetricQueries.CurrentMonth(DateTime.UtcNow);
        var postings = await DashboardMetricQueries.LoadProfitAndLossPostingsAsync(_context, tenantId, farmId, start, endExclusive, cancellationToken);
        return AccountChart.Revenue(postings);
    }

    public async Task<decimal> GetCurrentMonthExpenseAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DashboardMetricQueries.CurrentMonth(DateTime.UtcNow);
        var postings = await DashboardMetricQueries.LoadProfitAndLossPostingsAsync(_context, tenantId, farmId, start, endExclusive, cancellationToken);
        return AccountChart.Expenses(postings);
    }

    public async Task<int> GetBirthsThisMonthAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DashboardMetricQueries.CurrentMonth(DateTime.UtcNow);
        var from = DateOnly.FromDateTime(start);
        var to = DateOnly.FromDateTime(endExclusive);

        var query = _context.Animals.AsNoTracking().Where(a => a.TenantId == tenantId
            && a.AcquisitionType == AcquisitionType.BornOnFarm
            && a.DateOfBirth >= from && a.DateOfBirth < to);
        if (farmId.HasValue)
        {
            query = query.Where(a => a.FarmId == farmId.Value);
        }
        return await query.CountAsync(cancellationToken);
    }

    /// <summary>Animals whose recorded <c>DeathDate</c> falls in the current calendar month.</summary>
    public async Task<int> GetDeathsThisMonthAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var (start, endExclusive) = DashboardMetricQueries.CurrentMonth(DateTime.UtcNow);
        var from = DateOnly.FromDateTime(start);
        var to = DateOnly.FromDateTime(endExclusive);

        var query = _context.Animals.AsNoTracking().Where(a => a.TenantId == tenantId
            && a.Status == AnimalStatus.Dead
            && a.DeathDate != null && a.DeathDate >= from && a.DeathDate < to);
        if (farmId.HasValue)
        {
            query = query.Where(a => a.FarmId == farmId.Value);
        }
        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> GetDueVaccinationsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var dueBy = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

        // Due within the next 7 days or overdue — only for animals still on the farm (a sold or
        // dead animal's open schedule is not work anyone can do).
        var onFarm = DashboardMetricQueries.OnFarmAnimals(_context, tenantId, farmId);
        return await (from v in _context.VaccinationEvents.AsNoTracking()
                      join a in onFarm on v.AnimalId equals a.Id
                      where v.TenantId == tenantId
                            && (v.Status == Domain.Health.Enums.VaccinationStatus.Scheduled || v.Status == Domain.Health.Enums.VaccinationStatus.Overdue)
                            && v.ScheduledDate <= dueBy
                      select v.Id).CountAsync(cancellationToken);
    }

    public async Task<int> GetPregnantAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        // Distinct on-farm dams with a confirmed, not-yet-calved pregnancy.
        var onFarm = DashboardMetricQueries.OnFarmAnimals(_context, tenantId, farmId);
        return await (from br in _context.BreedingRecords.AsNoTracking()
                      join a in onFarm on br.AnimalId equals a.Id
                      where br.IsPregnancyConfirmed && br.ActualCalvingDate == null
                      select br.AnimalId).Distinct().CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActionableInsight>> GetActiveInsightsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var query = _context.ActionableInsights.AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDismissed && !i.IsRead);
            
        if (farmId.HasValue)
        {
            query = query.Where(i => i.FarmId == farmId.Value);
        }
        
        return await query.OrderByDescending(i => i.Severity).ThenByDescending(i => i.CreatedAtUtc).Take(10).ToListAsync(cancellationToken);
    }
}
