using Farm360.Domain.MasterData;
using Farm360.Domain.MasterData.Enums;
using Farm360.Domain.MasterData.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.MasterData;

public class MasterDataRepository : IMasterDataRepository
{
    private readonly ApplicationDbContext _context;

    public MasterDataRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MasterDataEntry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MasterDataEntries
            .FirstOrDefaultAsync(m => m.Id == id && (m.TenantId == tenantId || m.TenantId == Guid.Empty), cancellationToken);
    }

    public async Task<MasterDataEntry?> GetByCodeAsync(Guid tenantId, MasterDataType type, string code, CancellationToken cancellationToken = default)
    {
        return await _context.MasterDataEntries
            .FirstOrDefaultAsync(m => (m.TenantId == tenantId || m.TenantId == Guid.Empty) && m.Type == type && m.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<MasterDataEntry>> GetAllByTypeAsync(Guid tenantId, MasterDataType type, CancellationToken cancellationToken = default)
    {
        return await _context.MasterDataEntries
            .Where(m => (m.TenantId == tenantId || m.TenantId == Guid.Empty) && m.Type == type)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, MasterDataType type, string code, CancellationToken cancellationToken = default)
    {
        return await _context.MasterDataEntries
            .AnyAsync(m => (m.TenantId == tenantId || m.TenantId == Guid.Empty) && m.Type == type && m.Code == code, cancellationToken);
    }

    public void Add(MasterDataEntry entry)
    {
        _context.MasterDataEntries.Add(entry);
    }

    public void Update(MasterDataEntry entry)
    {
        _context.MasterDataEntries.Update(entry);
    }

    public void Delete(MasterDataEntry entry)
    {
        _context.MasterDataEntries.Remove(entry);
    }
}
