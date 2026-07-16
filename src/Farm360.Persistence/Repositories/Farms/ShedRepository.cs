using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Farms;

public sealed class ShedRepository : IShedRepository
{
    private readonly ApplicationDbContext _context;

    public ShedRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Shed?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Shed>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, cancellationToken);
    }

    public async Task<Shed?> GetByNumberAsync(Guid tenantId, Guid farmId, string shedNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Shed>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.FarmId == farmId && s.ShedNumber == shedNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<Shed>> GetAllByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Shed>()
            .Where(s => s.TenantId == tenantId && s.FarmId == farmId)
            .OrderBy(s => s.ShedName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNumberAsync(Guid tenantId, Guid farmId, string shedNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Shed>()
            .AnyAsync(s => s.TenantId == tenantId && s.FarmId == farmId && s.ShedNumber == shedNumber, cancellationToken);
    }

    public void Add(Shed shed)
    {
        _context.Set<Shed>().Add(shed);
    }

    public void Update(Shed shed)
    {
        _context.Set<Shed>().Update(shed);
    }

    public void Delete(Shed shed)
    {
        _context.Set<Shed>().Remove(shed);
    }
}
