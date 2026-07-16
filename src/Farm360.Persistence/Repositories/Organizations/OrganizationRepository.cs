using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Organizations;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly ApplicationDbContext _context;

    public OrganizationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> GetAllByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.TenantId == tenantId && o.Name == name, cancellationToken);
    }

    public void Add(Organization organization)
    {
        _context.Organizations.Add(organization);
    }

    public void Update(Organization organization)
    {
        _context.Organizations.Update(organization);
    }
}
