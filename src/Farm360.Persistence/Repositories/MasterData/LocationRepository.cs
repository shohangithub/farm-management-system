using Farm360.Domain.MasterData.Locations;
using Farm360.Domain.MasterData.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.MasterData;

public class LocationRepository : ILocationRepository
{
    private readonly ApplicationDbContext _context;

    public LocationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Country>> GetCountriesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Countries
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Division>> GetDivisionsAsync(Guid tenantId, Guid countryId, CancellationToken cancellationToken = default)
    {
        return await _context.Divisions
            .Where(d => d.TenantId == tenantId && d.CountryId == countryId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<District>> GetDistrictsAsync(Guid tenantId, Guid divisionId, CancellationToken cancellationToken = default)
    {
        return await _context.Districts
            .Where(d => d.TenantId == tenantId && d.DivisionId == divisionId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upazila>> GetUpazilasAsync(Guid tenantId, Guid districtId, CancellationToken cancellationToken = default)
    {
        return await _context.Upazilas
            .Where(u => u.TenantId == tenantId && u.DistrictId == districtId)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Union>> GetUnionsAsync(Guid tenantId, Guid upazilaId, CancellationToken cancellationToken = default)
    {
        return await _context.Unions
            .Where(u => u.TenantId == tenantId && u.UpazilaId == upazilaId)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Village>> GetVillagesAsync(Guid tenantId, Guid unionId, CancellationToken cancellationToken = default)
    {
        return await _context.Villages
            .Where(v => v.TenantId == tenantId && v.UnionId == unionId)
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }
}
