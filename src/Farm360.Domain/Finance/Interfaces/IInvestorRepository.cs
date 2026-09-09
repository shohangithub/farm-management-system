using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Finance.Interfaces;

public interface IInvestorRepository
{
    Task<Investor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Investor?> GetByIdWithTransactionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Investor>> GetByFarmIdAsync(Guid farmId, bool includeTransactions = false, CancellationToken cancellationToken = default);
    void Add(Investor investor);
    void Update(Investor investor);
    void Delete(Investor investor);
}
