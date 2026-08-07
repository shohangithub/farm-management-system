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
}
