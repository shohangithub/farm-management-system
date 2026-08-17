using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using Farm360.Domain.Identity.Repositories;
using Farm360.Persistence.Context;
using Farm360.Persistence.Interceptors;
using Farm360.Persistence.Permissions;
using Farm360.Persistence.Queries;
using Farm360.Persistence.Repositories;
using Farm360.Persistence.Repositories.Livestock;
using Farm360.Persistence.Repositories.MasterData;
using Farm360.Persistence.Repositories.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.MasterData.Repositories;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Persistence.Repositories.Organizations;
using Farm360.Domain.Farms.Repositories;
using Farm360.Persistence.Repositories.Farms;
using Farm360.Persistence.Repositories.Tenancy;
using Farm360.Persistence.Repositories.Identity;
using Farm360.Persistence.Seed;
using Farm360.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Persistence.Repositories.Feeding;

namespace Farm360.Persistence.DependencyInjection;

/// <summary>
/// Persistence layer DI registration.
/// Called from Farm360.Api → Program.cs
/// </summary>
public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found.");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });

            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        
        // Register Queries
        services.AddScoped<Farm360.Application.Analytics.Queries.IAnalyticsQueryService, AnalyticsQueryService>();

        // ── Permission service (Redis-cached, DB-backed) ──────────────────────
        services.AddScoped<IPermissionService, PermissionService>();

        // ── Livestock repositories ──────────────────────────────────────────────
        services.AddScoped<IAnimalRepository, AnimalRepository>();
        services.AddScoped<IAnimalBatchRepository, AnimalBatchRepository>();
        services.AddScoped<IBreedRepository, BreedRepository>();

        // ── Health repositories ─────────────────────────────────────────────────
        services.AddScoped<IVaccinationRepository, VaccinationRepository>();
        services.AddScoped<IMedicalTreatmentRepository, MedicalTreatmentRepository>();
        services.AddScoped<IDiseaseIncidentRepository, DiseaseIncidentRepository>();
        services.AddScoped<IMortalityRecordRepository, MortalityRecordRepository>();
        services.AddScoped<IVetVisitRepository, VetVisitRepository>();
        services.AddScoped<IHealthDashboardRepository, HealthDashboardRepository>();

        // ── Feeding repositories ────────────────────────────────────────────────
        services.AddScoped<IFeedIngredientRepository, FeedIngredientRepository>();
        services.AddScoped<IFeedFormulaRepository, FeedFormulaRepository>();
        services.AddScoped<IFeedingScheduleRepository, FeedingScheduleRepository>();
        services.AddScoped<IFeedConsumptionLogRepository, FeedConsumptionLogRepository>();
        services.AddScoped<IFeedingRuleSetRepository, FeedingRuleSetRepository>();
        services.AddScoped<IAnimalFeedingPlanRepository, AnimalFeedingPlanRepository>();
        services.AddScoped<IDailyFeedingEntryRepository, DailyFeedingEntryRepository>();
        services.AddScoped<IFeedingReconciliationRepository, FeedingReconciliationRepository>();

        // ── Inventory repositories ──────────────────────────────────────────────
        services.AddScoped<Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository, Farm360.Persistence.Repositories.Inventory.InventoryItemRepository>();
        services.AddScoped<Farm360.Domain.Inventory.Interfaces.Repositories.ISupplierRepository, Farm360.Persistence.Repositories.Inventory.SupplierRepository>();
        services.AddScoped<Farm360.Domain.Inventory.Interfaces.Repositories.IStockTransactionRepository, Farm360.Persistence.Repositories.Inventory.StockTransactionRepository>();
        services.AddScoped<Farm360.Domain.Inventory.Interfaces.Repositories.IPurchaseOrderRepository, Farm360.Persistence.Repositories.Inventory.PurchaseOrderRepository>();

        // ── Intelligence repositories ───────────────────────────────────────────
        services.AddScoped<Farm360.Domain.Intelligence.Interfaces.Repositories.IInsightRepository, Farm360.Persistence.Repositories.Intelligence.InsightRepository>();
        
        // ── Dashboard repositories ──────────────────────────────────────────────
        services.AddScoped<Farm360.Domain.Dashboard.Interfaces.IExecutiveDashboardRepository, Farm360.Persistence.Repositories.Dashboard.ExecutiveDashboardRepository>();

        // ── Finance repositories ──────────────────────────────────────────────
        services.AddScoped<Farm360.Application.Finance.Repositories.IFinancialTransactionRepository, Farm360.Persistence.Repositories.Finance.FinancialTransactionRepository>();

        // ── Farm repositories ───────────────────────────────────────────
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<IShedRepository, ShedRepository>();
        services.AddScoped<IPenRepository, PenRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();

        // ── Master Data repositories ────────────────────────────────────
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();

        // ── Tenancy & Identity repositories ──────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantUserRepository, TenantUserRepository>();

        // ── Cross-cutting services ────────────────────────────────────────────
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();

        // ── Data Seeder (transient — runs once at startup) ────────────────────
        services.AddTransient<DataSeeder>();

        return services;
    }
}
