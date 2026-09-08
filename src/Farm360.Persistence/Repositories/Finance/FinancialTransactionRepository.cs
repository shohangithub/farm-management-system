using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Finance;

public class FinancialTransactionRepository : IFinancialTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public FinancialTransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FinancialTransactions
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetAllByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.FinancialTransactions
            .Where(t => t.FarmId == farmId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetAllByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.FinancialTransactions
            .Where(t => t.BatchId == batchId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetAllByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.FinancialTransactions
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.FinancialTransactions.AddAsync(transaction, cancellationToken);
    }

    public Task UpdateAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        _context.FinancialTransactions.Update(transaction);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
    {
        _context.FinancialTransactions.Remove(transaction);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<FinancialTransaction> Items, int TotalCount, decimal TotalIncome, decimal TotalExpense)> GetPagedAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _context.FinancialTransactions.Where(t => t.FarmId == farmId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => 
                t.Description.Contains(search) || 
                t.Notes.Contains(search) || 
                t.ReferenceId.Contains(search));
        }

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (category.HasValue)
        {
            query = query.Where(t => t.Category == category.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= endDate.Value);
        }

        if (animalId.HasValue)
        {
            query = query.Where(t => t.AnimalId == animalId.Value);
        }

        if (batchId.HasValue)
        {
            query = query.Where(t => t.BatchId == batchId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalIncome = await query
            .Where(t => t.Type == Domain.Finance.Enums.TransactionType.Income)
            .SumAsync(t => (decimal?)t.AmountBdt, cancellationToken) ?? 0m;

        var totalExpense = await query
            .Where(t => t.Type == Domain.Finance.Enums.TransactionType.Expense)
            .SumAsync(t => (decimal?)t.AmountBdt, cancellationToken) ?? 0m;

        query = sortBy?.ToLowerInvariant() switch
        {
            "amount" => sortDesc ? query.OrderByDescending(t => t.AmountBdt) : query.OrderBy(t => t.AmountBdt),
            "category" => sortDesc ? query.OrderByDescending(t => t.Category) : query.OrderBy(t => t.Category),
            "type" => sortDesc ? query.OrderByDescending(t => t.Type) : query.OrderBy(t => t.Type),
            _ => sortDesc ? query.OrderByDescending(t => t.TransactionDate) : query.OrderBy(t => t.TransactionDate)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, totalIncome, totalExpense);
    }
}
