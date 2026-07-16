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
