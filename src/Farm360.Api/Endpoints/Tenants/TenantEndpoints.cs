using Farm360.Application.Tenants.Commands;
using MediatR;

namespace Farm360.Api.Endpoints.Tenants;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapPost("/onboard", async (OnboardTenantCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            // We return the Organization ID here because the frontend expects it to redirect
            // to the new organization's dashboard/detail page.
            return Results.Created($"/api/v1/organizations/{id}", new { Id = id });
        })
        // Intentionally authenticated-only: the caller has no tenant (and so no permissions) yet.
        // The handler allows one self-service tenant per user; rate limited like registration.
        .RequireRateLimiting(Farm360.Api.Configuration.RateLimitingSetup.RegistrationPolicy);

        // Tenant settings → Farm rules (/api/v1/settings/business-rules).
        Farm360.Api.Endpoints.BusinessRules.BusinessRulesSettingsEndpoints.MapBusinessRulesSettingsEndpoints(app);
    }
}
