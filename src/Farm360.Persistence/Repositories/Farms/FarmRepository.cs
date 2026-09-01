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
    
    public async Task<IReadOnlyList<Farm360.Domain.Common.LookupItem>> GetLookupsAsync(Guid tenantId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Farm>().AsNoTracking().Where(f => f.TenantId == tenantId && (int)f.Status == 1); // 1 = Active
        
        if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(f => f.FarmName)
            .Select(f => new Farm360.Domain.Common.LookupItem(f.Id, f.FarmName, f.BranchId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default)
    {
        return await _context.Farms
            .AnyAsync(f => f.TenantId == tenantId && f.FarmCode == farmCode, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetOccupancyByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var animalQuery = _context.Set<Farm360.Domain.Livestock.Animal>()
            .Where(a => a.TenantId == tenantId && (a.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Active || a.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Quarantined));

        var grouped = await animalQuery
            .GroupBy(a => a.FarmId)
            .Select(g => new { FarmId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FarmId, x => x.Count, cancellationToken);

        return grouped;
    }

    public async Task<int> GetOccupancyByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Farm360.Domain.Livestock.Animal>()
            .CountAsync(a => a.TenantId == tenantId && a.FarmId == farmId && (a.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Active || a.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Quarantined), cancellationToken);
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
