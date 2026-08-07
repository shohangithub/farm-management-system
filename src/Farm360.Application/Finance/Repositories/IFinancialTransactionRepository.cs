using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance;

namespace Farm360.Application.Finance.Repositories;

public interface IFinancialTransactionRepository
{
    Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialTransaction>> GetAllByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task DeleteAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
}
