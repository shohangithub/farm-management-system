using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Farms;

public sealed class FarmRepository : IFarmRepository
{
    private readonly ApplicationDbContext _context;

    public FarmRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Farm?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, cancellationToken);
    }

    public async Task<Farm?> GetByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.FarmCode == farmCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Farm>> GetAllByBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm>()
            .Where(b => b.TenantId == tenantId && b.BranchId == branchId)
            .OrderBy(b => b.FarmName)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<Farm>> GetAllByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm>()
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.FarmName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm>()
            .AnyAsync(b => b.TenantId == tenantId && b.FarmCode == farmCode, cancellationToken);
    }

    public void Add(Farm farm)
    {
        _context.Set<Farm>().Add(farm);
    }

    public void Update(Farm farm)
    {
        _context.Set<Farm>().Update(farm);
    }

    public void Delete(Farm farm)
    {
        _context.Set<Farm>().Remove(farm);
    }
}
