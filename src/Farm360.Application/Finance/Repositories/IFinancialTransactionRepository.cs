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
    Task<IReadOnlyList<FinancialTransaction>> GetAllByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialTransaction>> GetAllByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task DeleteAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FinancialTransaction> Items, int TotalCount, decimal TotalIncome, decimal TotalExpense)> GetPagedAsync(
        Guid farmId,
        int pageNumber,
        int pageSize,
        string? search,
        Domain.Finance.Enums.TransactionType? type,
        Domain.Finance.Enums.TransactionCategory? category,
        DateTime? startDate,
        DateTime? endDate,
        Guid? animalId,
        Guid? batchId,
        string? sortBy,
        bool sortDesc,
        bool? isAutomated = null,
        string? sourceModule = null,
        CancellationToken cancellationToken = default);
    Task<FinancialTransaction?> GetAnimalPurchaseTransactionAsync(Guid animalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Farm-level expenses in a period that are not already attributed to a single animal —
    /// the input to overhead allocation (docs/32 GAP-2).
    /// </summary>
    /// <remarks>
    /// Excludes rows that already carry an <c>AnimalId</c>: those are direct costs and the
    /// module that recorded them has already put them on the animal's ledger. Allocating them
    /// again would double-count.
    /// </remarks>
    Task<IReadOnlyList<FinancialTransaction>> GetUnattributedIndirectCostsAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
