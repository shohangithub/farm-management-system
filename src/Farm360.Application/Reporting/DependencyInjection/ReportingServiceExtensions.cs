using System;
using System.Linq;
using System.Reflection;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Registry;
using Farm360.Application.Reporting.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Farm360.Application.Reporting.DependencyInjection;

public static class ReportingServiceExtensions
{
    /// <summary>
    /// Discovers every <see cref="IReportDefinition"/> in the Application assembly and builds the
    /// registry. Adding a report is adding a class — there is no list to remember to update,
    /// which is the difference between a platform and thirty hand-wired pages.
    /// </summary>
    public static IServiceCollection AddReportingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = Assembly.GetExecutingAssembly();

        var definitionTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                        && typeof(IReportDefinition).IsAssignableFrom(t));

        foreach (var type in definitionTypes)
        {
            services.AddScoped(typeof(IReportDefinition), type);
        }

        // Scoped, not singleton. Definitions may inject repositories to fetch their data, and a
        // singleton registry holding them would capture a scoped DbContext for the life of the
        // process — the classic captive-dependency bug. Building ~30 metadata objects per request
        // is far cheaper than the leak it avoids.
        services.AddScoped<IReportRegistry, ReportRegistry>();

        // Execution is per-request: it resolves tenant, user and DbContext-scoped services.
        services.AddScoped<IReportExecutionService, ReportExecutionService>();

        return services;
    }
}
