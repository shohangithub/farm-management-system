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
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        // ── FluentValidation (auto-discover all validators in assembly) ───────
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<Farm360.Application.Intelligence.Interfaces.ICostAndProfitEngine, Farm360.Application.Intelligence.Services.CostAndProfitEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Interfaces.IGrowthPredictionEngine, Farm360.Application.Intelligence.Services.GrowthPredictionEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Interfaces.IRuleEngine, Farm360.Application.Intelligence.Services.RuleEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Interfaces.ISimulationEngine, Farm360.Application.Intelligence.Services.SimulationEngine>();
        services.AddScoped<Farm360.Application.Intelligence.Services.IProjectionDefaultsResolver, Farm360.Application.Intelligence.Services.ProjectionDefaultsResolver>();

        // ── AutoMapper (auto-discover all mapping profiles in assembly) ────────
        services.AddAutoMapper(assembly);

        return services;
    }
}
