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

public class AnimalCostLedgerRepository : IAnimalCostLedgerRepository
{
    private readonly ApplicationDbContext _context;

    public AnimalCostLedgerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AnimalCostLedger?> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        return await _context.AnimalCostLedgers
            .FirstOrDefaultAsync(l => l.AnimalId == animalId, cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalCostLedger>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.AnimalCostLedgers
            .Where(l => l.FarmId == farmId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AnimalCostLedger>> GetByBatchIdAsync(Guid farmId, Guid batchId, CancellationToken cancellationToken = default)
    {
        // Join with Animals table to filter by BatchId
        var animalIds = await _context.Animals
            .Where(a => a.FarmId == farmId && a.BatchId == batchId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        return await _context.AnimalCostLedgers
            .Where(l => animalIds.Contains(l.AnimalId))
            .ToListAsync(cancellationToken);
    }

    public void Add(AnimalCostLedger ledger)
    {
        _context.AnimalCostLedgers.Add(ledger);
    }

    public void Update(AnimalCostLedger ledger)
    {
        _context.AnimalCostLedgers.Update(ledger);
    }
}
