using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>
/// A list-backed ledger so posting tests exercise the real posting rules (idempotency, revise,
/// reverse) instead of verifying mock calls.
/// </summary>
internal sealed class InMemoryFinancialTransactionRepository : IFinancialTransactionRepository
{
    public List<FinancialTransaction> Rows { get; } = [];

    public IReadOnlyList<FinancialTransaction> Of(TransactionCategory category) =>
        Rows.Where(r => r.Category == category).ToList();

    public Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<FinancialTransaction>> GetAllByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>(Rows.Where(r => r.FarmId == farmId).ToList());

    public Task<IReadOnlyList<FinancialTransaction>> GetAllByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>(Rows.Where(r => r.BatchId == batchId).ToList());

    public Task<IReadOnlyList<FinancialTransaction>> GetAllByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>(Rows.Where(r => r.TenantId == tenantId).ToList());

    public Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        Rows.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        Rows.Remove(transaction);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<FinancialTransaction> Items, int TotalCount, decimal TotalIncome, decimal TotalExpense)> GetPagedAsync(
        Guid farmId, int pageNumber, int pageSize, string? search, TransactionType? type, TransactionCategory? category,
        DateTime? startDate, DateTime? endDate, Guid? animalId, Guid? batchId, string? sortBy, bool sortDesc,
        bool? isAutomated = null, string? sourceModule = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<FinancialTransaction?> GetAnimalPurchaseTransactionAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.AnimalId == animalId && r.Category == TransactionCategory.AnimalPurchase));

    public Task<FinancialTransaction?> FindBySourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.FarmId == farmId && r.SourceDocumentId == sourceDocumentId && r.Category == category));

    public Task<IReadOnlyList<FinancialTransaction>> GetBySourceAsync(Guid farmId, Guid sourceDocumentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>(Rows.Where(r => r.FarmId == farmId && r.SourceDocumentId == sourceDocumentId).ToList());

    public Task<IReadOnlyList<FinancialTransaction>> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>(Rows.Where(r => r.AnimalId == animalId).ToList());

    public Task<IReadOnlyList<FinancialTransaction>> GetUnattributedIndirectCostsAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinancialTransaction>>([]);
}
