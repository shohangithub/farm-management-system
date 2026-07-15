using Farm360.Application.Organizations.Commands;
using Farm360.Application.Organizations.Queries;
using Farm360.Persistence.Seed;
using MediatR;

namespace Farm360.Api.Endpoints.Organizations;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organizations")
            .WithTags("Organizations")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetOrganizationsQuery());
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.OrganizationModule.View));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetOrganizationByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.OrganizationModule.View));

        group.MapPost("/", async (CreateOrganizationCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/organizations/{id}", new { Id = id });
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.OrganizationModule.Create));

        group.MapPut("/{id:guid}", async (Guid id, UpdateOrganizationCommand command, ISender sender) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID mismatch.");

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.OrganizationModule.Edit));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateOrganizationCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.OrganizationModule.Delete));
    }
}
