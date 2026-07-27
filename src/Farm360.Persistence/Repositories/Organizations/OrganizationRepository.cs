using Farm360.Domain.Common;
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

    public async Task<(IReadOnlyList<Organization> Items, int TotalCount)> GetPagedByTenantAsync(
        Guid tenantId, 
        string? searchTerm, 
        int? status, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Organizations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(o => o.Name.Contains(searchTerm) || o.ContactEmail.Contains(searchTerm));
        }

        if (status.HasValue)
        {
            query = query.Where(o => (int)o.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(o => o.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && (int)o.Status == 1) // 1 = Active
            .OrderBy(o => o.Name)
            .Select(o => new LookupItem(o.Id, o.Name, null))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.TenantId == tenantId && o.Name == name, cancellationToken);
    }

    public async Task<Organization?> GetByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        // EF global query filters (tenant + soft-delete) are active; the TenantId check is explicit for clarity
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Name == name, cancellationToken);
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
