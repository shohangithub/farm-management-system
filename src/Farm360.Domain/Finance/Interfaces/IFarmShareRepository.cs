using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

public interface IFarmShareRepository
{
    Task<FarmShareConfig?> GetConfigByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    void AddConfig(FarmShareConfig config);
    void UpdateConfig(FarmShareConfig config);

    Task<ShareHolding?> GetHoldingByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShareHolding?> GetHoldingByInvestorAndFarmAsync(Guid investorId, Guid farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareHolding>> GetHoldingsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareHolding>> GetHoldingsByInvestorIdAsync(Guid investorId, CancellationToken cancellationToken = default);
    void AddHolding(ShareHolding holding);
    void UpdateHolding(ShareHolding holding);

    Task<IReadOnlyList<ShareTransaction>> GetTransactionsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShareTransaction>> GetTransactionsByInvestorIdAsync(Guid investorId, CancellationToken cancellationToken = default);
    void AddTransaction(ShareTransaction transaction);
}
