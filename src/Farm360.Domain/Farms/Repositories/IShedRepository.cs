namespace Farm360.Domain.Farms.Repositories;

public interface IShedRepository
{
    Task<Shed?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<Shed?> GetByNumberAsync(Guid tenantId, Guid farmId, string shedNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Shed>> GetAllByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNumberAsync(Guid tenantId, Guid farmId, string shedNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetOccupancyByFarmAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);
    Task<int> GetOccupancyByShedAsync(Guid tenantId, Guid shedId, CancellationToken cancellationToken = default);
    
    void Add(Shed shed);
    void Update(Shed shed);
    void Delete(Shed shed);
}
