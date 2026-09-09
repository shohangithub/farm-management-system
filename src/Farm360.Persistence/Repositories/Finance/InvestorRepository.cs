using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Finance;

public class InvestorRepository : IInvestorRepository
{
    private readonly ApplicationDbContext _context;

    public InvestorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Investor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Investors
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Investor?> GetByIdWithTransactionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Investors
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Investor>> GetByFarmIdAsync(Guid farmId, bool includeTransactions = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Investor> query = _context.Investors
            .Where(i => i.FarmId == farmId);

        if (includeTransactions)
        {
            query = query.Include(i => i.Transactions);
        }

        return await query
            .OrderByDescending(i => i.TotalInvestedBdt)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public void Add(Investor investor)
    {
        _context.Investors.Add(investor);
    }

    public void Update(Investor investor)
    {
        _context.Investors.Update(investor);
    }

    public void Delete(Investor investor)
    {
        _context.Investors.Remove(investor);
    }
}
