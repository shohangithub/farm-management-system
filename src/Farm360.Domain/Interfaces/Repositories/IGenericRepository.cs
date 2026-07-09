using Farm360.Shared.Primitives;

namespace Farm360.Domain.Interfaces.Repositories;

/// <summary>
/// Generic repository interface for aggregate roots.
/// Constitution §8 (CQRS): Repositories are on write side only.
/// Read side uses direct EF Core queries in handlers (no repository abstraction).
/// Only aggregate roots have repositories — never internal entities.
/// </summary>
/// <typeparam name="TEntity">Must be an aggregate root type.</typeparam>
public interface IGenericRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
