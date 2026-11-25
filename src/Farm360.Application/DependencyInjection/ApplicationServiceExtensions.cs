using Farm360.Application.Reporting.DependencyInjection;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Farm360.Application.DependencyInjection;

/// <summary>
/// Application layer DI registration.
/// Constitution §3: DI registered in each layer's own ServiceExtensions class.
/// Called from Farm360.Api → Program.cs as: builder.Services.AddApplicationServices()
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // ── MediatR (CQRS) ───────────────────────────────────────────────────
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline order (outermost to innermost — Constitution §8.3):
            // [1] Logging → [2] Performance → [3] Validation → [4] Transaction → [5] Caching → Handler
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(FarmAccessBehavior<,>)); // per-farm access for requests carrying a FarmId
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        // ── FluentValidation (auto-discover all validators in assembly) ───────
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Tenant-local business calendar ("today" in the farm's zone, not UTC).
        services.AddSingleton<Farm360.Application.Common.Time.FarmTimeZoneCache>();
        services.AddScoped<Farm360.Application.Common.Interfaces.IFarmClock, Farm360.Application.Common.Time.FarmClock>();
        services.AddScoped<Farm360.Application.Common.Jobs.ITenantJobRunner, Farm360.Application.Common.Jobs.TenantJobRunner>();

        services.AddScoped<Farm360.Application.Intelligence.Interfaces.IGrowthPredictionEngine, Farm360.Application.Intelligence.Services.GrowthPredictionEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Interfaces.IRuleEngine, Farm360.Application.Intelligence.Services.RuleEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Interfaces.ISimulationEngine, Farm360.Application.Intelligence.Services.SimulationEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Services.IProjectionDefaultsResolver, Farm360.Application.Intelligence.Services.ProjectionDefaultsResolver>();
        services.AddScoped<Farm360.Application.Intelligence.Services.IAnimalValuationService, Farm360.Application.Intelligence.Services.AnimalValuationService>();

        // ── Finance: labour and overhead allocation (docs/32 GAP-2) ──────────
        services.AddScoped<Farm360.Application.Finance.Services.IOverheadAllocationService, Farm360.Application.Finance.Services.OverheadAllocationService>();
        services.AddScoped<Farm360.Application.Finance.Services.ILedgerPostingService, Farm360.Application.Finance.Services.LedgerPostingService>();
        services.AddScoped<Farm360.Application.Finance.Services.ILivestockAccountingService, Farm360.Application.Finance.Services.LivestockAccountingService>();
        services.AddScoped<Farm360.Application.Finance.Services.IAccountingReconciliationService, Farm360.Application.Finance.Services.AccountingReconciliationService>();
        services.AddScoped<Farm360.Application.Finance.Services.ITradeCreditService, Farm360.Application.Finance.Services.TradeCreditService>();
        services.AddScoped<Farm360.Application.Finance.Services.IFinancialStatementEngine, Farm360.Application.Finance.Services.FinancialStatementEngine>();

        // ── Feeding: per-animal feed allocation (docs/32 GAP-1) ──────────────
        services.AddScoped<Farm360.Application.Feeding.Services.IFeedAllocationService, Farm360.Application.Feeding.Services.FeedAllocationService>();
        services.AddScoped<Farm360.Application.Feeding.Services.IDirectFeedService, Farm360.Application.Feeding.Services.DirectFeedService>();
        services.AddScoped<Farm360.Application.Feeding.Services.IAnimalExitService, Farm360.Application.Feeding.Services.AnimalExitService>();
        services.AddScoped<Farm360.Application.Inventory.Units.IUnitService, Farm360.Application.Inventory.Units.UnitService>();

        // ── Users / team management: shared guards for member commands ───────
        services.AddScoped<Farm360.Application.Users.Commands.MemberCommandContext>();
        services.AddScoped<Farm360.Application.Users.Commands.InvitationSender>();

        // ── Reporting platform (docs/32): registry + execution service ────────
        services.AddReportingServices();

        // ── Customer documents: invoices, receipts, purchase orders (share the report letterhead) ──
        services.AddScoped<Farm360.Application.Documents.IDocumentHeaderBuilder, Farm360.Application.Documents.DocumentHeaderBuilder>();

        // ── Public "Try the demo" farm (Demo:Enabled) ────────────────────────
        services.AddScoped<Farm360.Application.Demo.DemoSeeder>();
        services.AddScoped<Farm360.Application.Demo.DemoTenantManager>();
        services.AddScoped<Farm360.Application.Demo.DemoTenantPurger>();

        // ── AutoMapper (auto-discover all mapping profiles in assembly) ────────
        services.AddAutoMapper(assembly);

        return services;
    }
}
