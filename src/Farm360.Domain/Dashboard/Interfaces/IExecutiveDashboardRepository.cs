using Farm360.Domain.Intelligence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Dashboard.Interfaces;

public interface IExecutiveDashboardRepository
{
    Task<int> GetTotalAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
    Task<int> GetSickAnimalsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
    Task<int> GetFeedLowStockCountAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentMonthIncomeAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentMonthExpenseAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActionableInsight>> GetActiveInsightsAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken = default);
}
