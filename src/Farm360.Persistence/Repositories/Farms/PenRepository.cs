using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Farms;

public class PenRepository : IPenRepository
{
    private readonly ApplicationDbContext _context;

    public PenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Pen?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Pens
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);
    }

    public async Task<Pen?> GetByNumberAsync(Guid tenantId, Guid shedId, string penNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Pens
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ShedId == shedId && p.PenNumber == penNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<Pen>> GetAllByShedAsync(Guid tenantId, Guid shedId, CancellationToken cancellationToken = default)
    {
        return await _context.Pens
            .Where(p => p.TenantId == tenantId && p.ShedId == shedId)
            .OrderBy(p => p.PenNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNumberAsync(Guid tenantId, Guid shedId, string penNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Pens
            .AnyAsync(p => p.TenantId == tenantId && p.ShedId == shedId && p.PenNumber == penNumber, cancellationToken);
    }

    public void Add(Pen pen)
    {
        _context.Pens.Add(pen);
    }

    public void Update(Pen pen)
    {
        _context.Pens.Update(pen);
    }

    public void Delete(Pen pen)
    {
        _context.Pens.Remove(pen);
    }
}
