using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

public interface IMoneyAccountRepository
{
    Task<MoneyAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MoneyAccount>> GetByFarmIdAsync(Guid farmId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>The farm's default account, including one added earlier in the same unit of work.</summary>
    Task<MoneyAccount?> GetDefaultAsync(Guid farmId, CancellationToken cancellationToken = default);

    void Add(MoneyAccount account);
    void Update(MoneyAccount account);
}
