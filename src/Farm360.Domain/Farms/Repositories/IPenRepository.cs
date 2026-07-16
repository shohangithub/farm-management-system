namespace Farm360.Domain.Farms.Repositories;

public interface IPenRepository
{
    Task<Pen?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<Pen?> GetByNumberAsync(Guid tenantId, Guid shedId, string penNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pen>> GetAllByShedAsync(Guid tenantId, Guid shedId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNumberAsync(Guid tenantId, Guid shedId, string penNumber, CancellationToken cancellationToken = default);
    
    void Add(Pen pen);
    void Update(Pen pen);
    void Delete(Pen pen);
}
