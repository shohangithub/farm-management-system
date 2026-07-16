using Farm360.Domain.MasterData.Locations;

namespace Farm360.Domain.MasterData.Repositories;

public interface ILocationRepository
{
    Task<IReadOnlyList<Country>> GetCountriesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Division>> GetDivisionsAsync(Guid tenantId, Guid countryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<District>> GetDistrictsAsync(Guid tenantId, Guid divisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Upazila>> GetUpazilasAsync(Guid tenantId, Guid districtId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Union>> GetUnionsAsync(Guid tenantId, Guid upazilaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Village>> GetVillagesAsync(Guid tenantId, Guid unionId, CancellationToken cancellationToken = default);
    
    // Additional methods for add/update/delete could be added if full location management is needed
}
