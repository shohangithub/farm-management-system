using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Farm360.Persistence.Repositories;

/// <summary>
/// Generic repository — provides CRUD + paged query operations.
/// Constitution §8 (CQRS): Repositories are for WRITES and single-entity READS only.
/// For complex queries/reports, handlers use DbContext directly (no abstraction overhead).
/// F360-MTA-2026-001: Tenant isolation enforced at DbContext via GlobalQueryFilter — no repository code needed.
/// </summary>
public sealed class GenericRepository<T>(ApplicationDbContext context)
    : IGenericRepository<T>
    where T : AuditableEntity
{
    private readonly DbSet<T> _dbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> GetWhereAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(predicate, cancellationToken);

    public async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? await _dbSet.CountAsync(cancellationToken)
            : await _dbSet.CountAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _dbSet.AddAsync(entity, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        await _dbSet.AddRangeAsync(entities, cancellationToken);

    public void Update(T entity) =>
        _dbSet.Update(entity);

    public void Remove(T entity) =>
        // Constitution §12: SoftDelete — GlobalQueryFilter hides the record; interceptor converts any hard-delete attempt
        entity.SoftDelete(Guid.Empty); // Caller should set real userId via AuditInterceptor
}
