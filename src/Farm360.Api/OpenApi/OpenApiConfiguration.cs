using Microsoft.Extensions.DependencyInjection;

namespace Farm360.Api.OpenApi;

/// <summary>
/// OpenAPI configuration.
/// Constitution §6 (API Standards): All APIs documented via OpenAPI/Swagger.
/// Uses native .NET 10 OpenAPI integration (replaces Swashbuckle).
/// </summary>
public static class OpenApiConfiguration
{
    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }
}
