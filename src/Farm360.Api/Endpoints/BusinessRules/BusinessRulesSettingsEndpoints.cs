using Farm360.Api.Authorization;
using Farm360.Application.BusinessRules;
using Farm360.Domain.BusinessRules;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.BusinessRules;

/// <summary>
/// Settings → Farm rules: the organisation's own business assumptions (valuation, feed and overhead
/// allocation, report language) layered over the platform defaults.
/// </summary>
public static class BusinessRulesSettingsEndpoints
{
    private const string FutureOnlyNote =
        "Changes apply to future calculations only: feed and overhead allocations already recorded keep " +
        "the method and prices that produced them, so previously issued reports do not change.";

    public static IEndpointRouteBuilder MapBusinessRulesSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings/business-rules")
            .WithTags("Settings")
            .RequireAuthorization();

        group.MapGet("", async ([FromQuery] Guid? farmId, ISender sender) =>
                Results.Ok(await sender.Send(new GetBusinessRulesSettingsQuery(farmId))))
            .RequirePermission(PermissionConstants.Settings.View)
            .WithSummary("Get the organisation's business rules (or one farm's overrides with ?farmId=).")
            .WithDescription("Returns the effective values, the inherited defaults and the stored overrides. " + FutureOnlyNote)
            .Produces<BusinessRulesSettingsDto>();

        group.MapPut("", async ([FromQuery] Guid? farmId, [FromBody] BusinessRulesOverrides values, ISender sender) =>
                Results.Ok(await sender.Send(new SaveBusinessRulesSettingsCommand(farmId, values))))
            .RequirePermission(PermissionConstants.Settings.Edit)
            .WithSummary("Save the organisation's business rules (or one farm's overrides with ?farmId=).")
            .WithDescription("A null field inherits the default; all fields null resets to defaults. " + FutureOnlyNote)
            .Produces<BusinessRulesSettingsDto>()
            .ProducesValidationProblem();

        return app;
    }
}
