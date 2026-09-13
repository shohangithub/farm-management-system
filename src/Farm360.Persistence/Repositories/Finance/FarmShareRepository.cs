using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Finance;

public class FarmShareRepository : IFarmShareRepository
{
    private readonly ApplicationDbContext _context;

    public FarmShareRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FarmShareConfig?> GetConfigByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.FarmShareConfigs
            .FirstOrDefaultAsync(c => c.FarmId == farmId, cancellationToken);
    }

    public void AddConfig(FarmShareConfig config)
    {
        _context.FarmShareConfigs.Add(config);
    }

    public void UpdateConfig(FarmShareConfig config)
    {
        _context.FarmShareConfigs.Update(config);
    }

    public async Task<ShareHolding?> GetHoldingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShareHoldings
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<ShareHolding?> GetHoldingByInvestorAndFarmAsync(Guid investorId, Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.ShareHoldings
            .FirstOrDefaultAsync(h => h.InvestorId == investorId && h.FarmId == farmId, cancellationToken);
    }

    public async Task<IReadOnlyList<ShareHolding>> GetHoldingsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.ShareHoldings
            .Where(h => h.FarmId == farmId)
            .OrderByDescending(h => h.ShareCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareHolding>> GetHoldingsByInvestorIdAsync(Guid investorId, CancellationToken cancellationToken = default)
    {
        return await _context.ShareHoldings
            .Where(h => h.InvestorId == investorId)
            .OrderByDescending(h => h.ShareCount)
            .ToListAsync(cancellationToken);
    }

    public void AddHolding(ShareHolding holding)
    {
        _context.ShareHoldings.Add(holding);
    }

    public void UpdateHolding(ShareHolding holding)
    {
        _context.ShareHoldings.Update(holding);
    }

    public async Task<IReadOnlyList<ShareTransaction>> GetTransactionsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.ShareTransactions
            .Where(t => t.FarmId == farmId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareTransaction>> GetTransactionsByInvestorIdAsync(Guid investorId, CancellationToken cancellationToken = default)
    {
        return await _context.ShareTransactions
            .Where(t => t.InvestorId == investorId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void AddTransaction(ShareTransaction transaction)
    {
        _context.ShareTransactions.Add(transaction);
    }
}
