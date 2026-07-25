using Farm360.Application.Organizations.Branches.Commands;
using Farm360.Application.Organizations.Branches.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Organizations;

public static class BranchEndpoints
{
    public static IEndpointRouteBuilder MapBranchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/organizations/{orgId:guid}/branches")
            .WithTags("Branches")
            .RequireAuthorization();

        var rootGroup = app.MapGroup("/api/v1/branches")
            .WithTags("Branches")
            .RequireAuthorization();

        group.MapGet("/", async ([FromRoute] Guid orgId, ISender sender) =>
        {
            var result = await sender.Send(new GetBranchesByOrganizationQuery(orgId));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.View}");

        rootGroup.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetBranchByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.View}");

        group.MapPost("/", async ([FromRoute] Guid orgId, [FromBody] CreateBranchCommand command, ISender sender) =>
        {
            if (orgId != command.OrganizationId)
            {
                return Results.BadRequest("OrganizationId in route does not match command.");
            }

            var id = await sender.Send(command);
            return Results.Created($"/api/v1/branches/{id}", new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Create}");

        rootGroup.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateBranchCommand command, ISender sender) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest("Id in route does not match command.");
            }

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Edit}");

        rootGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteBranchCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.OrganizationModule.Delete}");

        return app;
    }
}
