using Farm360.Domain.Dashboard.Interfaces;
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

    public async Task<int> GetTotalAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var query = _context.Animals.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (farmId.HasValue)
        {
            query = query.Where(a => a.FarmId == farmId.Value);
        }
        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> GetSickAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var query = _context.DiseaseIncidents.AsNoTracking().Where(i => i.TenantId == tenantId && i.Status != Domain.Health.Enums.IncidentStatus.Resolved);
        if (farmId.HasValue)
        {
            query = query.Where(i => i.FarmId == farmId.Value);
        }
        
        // Sum affected animal count across all active incidents. This is an approximation
        // as one animal might be affected by multiple incidents, but sufficient for a high-level dashboard.
        return await query.SumAsync(i => i.AffectedAnimalCount, cancellationToken);
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

    public async Task<decimal> GetCurrentMonthIncomeAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = _context.FinancialTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId 
                        && t.Type == Domain.Finance.Enums.TransactionType.Income
                        && t.TransactionDate >= startOfMonth);
                        
        if (farmId.HasValue)
        {
            query = query.Where(t => t.FarmId == farmId.Value);
        }
        
        return await query.SumAsync(t => t.AmountBdt, cancellationToken);
    }

    public async Task<decimal> GetCurrentMonthExpenseAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = _context.FinancialTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId 
                        && t.Type == Domain.Finance.Enums.TransactionType.Expense
                        && t.TransactionDate >= startOfMonth);
                        
        if (farmId.HasValue)
        {
            query = query.Where(t => t.FarmId == farmId.Value);
        }
        
        return await query.SumAsync(t => t.AmountBdt, cancellationToken);
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
