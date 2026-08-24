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

public class LoanRecordRepository : ILoanRecordRepository
{
    private readonly ApplicationDbContext _context;

    public LoanRecordRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LoanRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRecords
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanRecord>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRecords
            .Where(l => l.FarmId == farmId)
            .OrderByDescending(l => l.DisbursementDate)
            .ToListAsync(cancellationToken);
    }

    public void Add(LoanRecord loan)
    {
        _context.LoanRecords.Add(loan);
    }

    public void Update(LoanRecord loan)
    {
        _context.LoanRecords.Update(loan);
    }

    public void Delete(LoanRecord loan)
    {
        _context.LoanRecords.Remove(loan);
    }
}
