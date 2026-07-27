using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Organizations;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ApplicationDbContext _context;

    public BranchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Branch>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, cancellationToken);
    }

    public async Task<Branch?> GetByCodeAsync(Guid tenantId, string branchCode, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Branch>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.BranchCode == branchCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> GetAllByOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Branch>()
            .Where(b => b.TenantId == tenantId && b.OrganizationId == organizationId)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Branch> Items, int TotalCount)> GetPagedByOrganizationAsync(
        Guid tenantId, 
        Guid organizationId, 
        string? searchTerm, 
        int? status, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Branch>().Where(b => b.TenantId == tenantId && b.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b => b.Name.Contains(searchTerm) || b.BranchCode.Contains(searchTerm));
        }

        if (status.HasValue)
        {
            query = query.Where(b => (int)b.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(b => b.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Farm360.Domain.Common.LookupItem>> GetLookupsAsync(Guid tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Branch>().AsNoTracking().Where(b => b.TenantId == tenantId && (int)b.Status == 1); // 1 = Active
        
        if (organizationId.HasValue)
        {
            query = query.Where(b => b.OrganizationId == organizationId.Value);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new Farm360.Domain.Common.LookupItem(b.Id, b.Name, b.OrganizationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string branchCode, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Branch>()
            .AnyAsync(b => b.TenantId == tenantId && b.BranchCode == branchCode, cancellationToken);
    }

    public void Add(Branch branch)
    {
        _context.Set<Branch>().Add(branch);
    }

    public void Update(Branch branch)
    {
        _context.Set<Branch>().Update(branch);
    }

    public void Delete(Branch branch)
    {
        _context.Set<Branch>().Remove(branch);
    }
}
