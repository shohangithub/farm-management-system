using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Farm360.Persistence.Context;

/// <summary>
/// EF Core wrapper for ITransaction — adapts IDbContextTransaction to the Application's ITransaction abstraction.
/// Clean Architecture: Application defines ITransaction; Persistence implements it with EF Core.
/// </summary>
internal sealed class EfCoreTransaction(IDbContextTransaction inner) : ITransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        inner.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        inner.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

/// <summary>
/// Main EF Core DbContext for all business data.
/// Constitution §12 (Database Standards): Schema-segregated tables.
/// F360-MTA-2026-001: Global Query Filters enforce tenant isolation (Layer 1).
///   - WHERE TenantId = @currentTenantId  (ITenantEntity filter)
///   - WHERE IsDeleted = 0                (ISoftDeletable filter)
/// NEVER disable these filters outside of admin/migration code.
/// </summary>
public class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantService _tenantService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    // ── Business module DbSets registered here when modules are implemented ─
    // Example (NOT implemented at scaffolding stage):
    // public DbSet<Animal> Animals => Set<Animal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-discover all IEntityTypeConfiguration<T> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ── Global Query Filter 1: Tenant Isolation ──────────────────────────
        // F360-MTA-2026-001 Layer 1: Developer cannot accidentally query across tenants.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var tenantId = _tenantService.TenantId;
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildTenantFilter(entityType.ClrType, tenantId));
            }

            // ── Global Query Filter 2: Soft Delete ───────────────────────────
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    // ── IUnitOfWork implementation ────────────────────────────────────────────
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var inner = await Database.BeginTransactionAsync(cancellationToken);
        return new EfCoreTransaction(inner);
    }

    public async Task CommitTransactionAsync(ITransaction transaction, CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(ITransaction transaction, CancellationToken cancellationToken = default)
    {
        await transaction.RollbackAsync(cancellationToken);
    }

    public new ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return base.DisposeAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static System.Linq.Expressions.LambdaExpression BuildTenantFilter(Type entityType, Guid tenantId)
    {
        var param = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var tenantIdProperty = System.Linq.Expressions.Expression.Property(param, nameof(ITenantEntity.TenantId));
        var tenantIdConstant = System.Linq.Expressions.Expression.Constant(tenantId);
        var body = System.Linq.Expressions.Expression.Equal(tenantIdProperty, tenantIdConstant);
        return System.Linq.Expressions.Expression.Lambda(body, param);
    }

    private static System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var param = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var isDeletedProperty = System.Linq.Expressions.Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
        var falseConstant = System.Linq.Expressions.Expression.Constant(false);
        var body = System.Linq.Expressions.Expression.Equal(isDeletedProperty, falseConstant);
        return System.Linq.Expressions.Expression.Lambda(body, param);
    }
}
