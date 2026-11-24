using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Dashboard.Interfaces;

/// <summary>Reads what a farm has already set up, for the onboarding checklist on the dashboard.</summary>
public interface IFarmSetupStatusRepository
{
    Task<FarmSetupCounts> GetCountsAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);
}
