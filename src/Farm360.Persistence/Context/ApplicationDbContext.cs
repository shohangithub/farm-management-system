using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Identity;
using Farm360.Domain.Livestock;
using Farm360.Domain.Health;
using Farm360.Domain.MasterData;
using Farm360.Domain.MasterData.Locations;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Organizations;
using Farm360.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

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
/// Constitution §12 (Database Standards): Schema-segregated tables (app.*).
///
/// F360-MTA-2026-001 Layer 1: Global Query Filters enforce tenant isolation.
///   FILTER 1: WHERE TenantId = @currentTenantId  (ITenantEntity)
///   FILTER 2: WHERE IsDeleted = 0                (ISoftDeletable)
///
/// CRITICAL BUG FIX: Tenant filter evaluates _tenantService.TenantId at QUERY TIME,
/// not at model creation time. This is required for a shared multi-tenant DbContext.
/// NEVER use a captured constant — it would return the same tenant for all requests.
///
/// NEVER disable these filters outside of system/migration/admin code.
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

    // ── Tenancy DbSets ────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Farms Module ──────────────────────────────────────────────────────────
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Shed> Sheds => Set<Shed>();
    public DbSet<Pen> Pens => Set<Pen>();

    // ── Master Data Module ────────────────────────────────────────────────────
    public DbSet<MasterDataEntry> MasterDataEntries => Set<MasterDataEntry>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Upazila> Upazilas => Set<Upazila>();
    public DbSet<Union> Unions => Set<Union>();
    public DbSet<Village> Villages => Set<Village>();

    // ── Identity / Authorization DbSets ──────────────────────────────────────
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();

    // ── Livestock Module ───────────────────────────────────────────────────────
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<BreedingRecord> BreedingRecords => Set<BreedingRecord>();
    public DbSet<AnimalPhoto> AnimalPhotos => Set<AnimalPhoto>();

    // ── Health & Veterinary Module ─────────────────────────────────────────────
    public DbSet<VaccinationProtocol> VaccinationProtocols => Set<VaccinationProtocol>();
    public DbSet<VaccinationEvent> VaccinationEvents => Set<VaccinationEvent>();
    public DbSet<MedicalTreatment> MedicalTreatments => Set<MedicalTreatment>();
    public DbSet<DiseaseIncident> DiseaseIncidents => Set<DiseaseIncident>();

    // ── Current tenant accessor (evaluated at query time — NOT at startup) ───
    private Guid CurrentTenantId => _tenantService.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-discover all IEntityTypeConfiguration<T> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ── Combined Global Query Filters: Tenant Isolation & Soft Delete ────────
        // F360-MTA-2026-001 Layer 1: Combines TenantId == CurrentTenantId AND IsDeleted == false
        // into a SINGLE HasQueryFilter call per entity type to prevent filter overwriting.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

            if (!isTenantEntity && !isSoftDeletable)
            {
                continue;
            }

            var param = Expression.Parameter(entityType.ClrType, "e");
            Expression? combinedExpression = null;

            if (isTenantEntity)
            {
                var contextRef = Expression.Constant(this);
                var currentTenantIdProp = Expression.Property(contextRef, nameof(CurrentTenantId));
                var tenantIdProp = Expression.Property(param, nameof(ITenantEntity.TenantId));
                var tenantFilterExpr = Expression.OrElse(
                    Expression.Equal(tenantIdProp, currentTenantIdProp),
                    Expression.Equal(tenantIdProp, Expression.Constant(Guid.Empty))
                );

                combinedExpression = tenantFilterExpr;
            }

            if (isSoftDeletable)
            {
                var isDeletedProp = Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
                var softDeleteFilterExpr = Expression.Equal(isDeletedProp, Expression.Constant(false));

                combinedExpression = combinedExpression is null
                    ? softDeleteFilterExpr
                    : Expression.AndAlso(combinedExpression, softDeleteFilterExpr);
            }

            if (combinedExpression is not null)
            {
                var filter = Expression.Lambda(combinedExpression, param);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
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
}
