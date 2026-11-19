using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Tenancy;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;

    public TenantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(entity, cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FindAsync([id], cancellationToken);
    }

    public void Update(Tenant entity)
    {
        _context.Tenants.Update(entity);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _context.Tenants, cancellationToken);
    }

    public async Task<(int UsersCount, int FarmsCount, int AnimalsCount)> GetTenantUsageCountsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var usersCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _context.TenantUsers.IgnoreQueryFilters(),
            u => u.TenantId == tenantId && !u.IsDeleted,
            cancellationToken);

        var farmsCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _context.Farms.IgnoreQueryFilters(),
            f => f.TenantId == tenantId && !f.IsDeleted,
            cancellationToken);

        var animalsCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _context.Animals.IgnoreQueryFilters(),
            a => a.TenantId == tenantId && !a.IsDeleted,
            cancellationToken);

        return (usersCount, farmsCount, animalsCount);
    }

    public async Task AcquireQuotaLockAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Only meaningful inside a transaction (TransactionBehavior): the UPDLOCK is held until
        // commit, serialising concurrent quota checks for this tenant.
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction is null)
        {
            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT TOP(1) [Id] FROM [app].[Tenants] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {tenantId}",
            cancellationToken);
    }
}
