using Farm360.Api.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Farm360.Api.DependencyInjection;

/// <summary>
/// API layer DI registration.
/// Called from Program.cs. Registers controllers, routing, OpenAPI.
/// </summary>
public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddRouting(options => options.LowercaseUrls = true);
        
        // CORS Policy (adjust for prod)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        services.AddOpenApiDocumentation();

        return services;
    }
}
