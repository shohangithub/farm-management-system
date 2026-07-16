using Farm360.Application.Farms.Commands;
using Farm360.Application.Farms.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Farms;

public static class FarmEndpoints
{
    public static IEndpointRouteBuilder MapFarmEndpoints(this IEndpointRouteBuilder app)
    {
        var branchGroup = app.MapGroup("/api/v1/branches/{branchId:guid}/farms")
            .WithTags("Farms")
            .RequireAuthorization();

        var rootGroup = app.MapGroup("/api/v1/farms")
            .WithTags("Farms")
            .RequireAuthorization();

        branchGroup.MapGet("/", async ([FromRoute] Guid branchId, ISender sender) =>
        {
            var result = await sender.Send(new GetFarmsByBranchQuery(branchId));
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.FarmModule.View));

        rootGroup.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetFarmByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.FarmModule.View));

        branchGroup.MapPost("/", async ([FromRoute] Guid branchId, [FromBody] CreateFarmCommand command, ISender sender) =>
        {
            if (branchId != command.BranchId)
            {
                return Results.BadRequest("BranchId in route does not match command.");
            }

            var id = await sender.Send(command);
            return Results.Created($"/api/v1/farms/{id}", new { Id = id });
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.FarmModule.Create));

        rootGroup.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateFarmCommand command, ISender sender) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest("Id in route does not match command.");
            }

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.FarmModule.Edit));

        rootGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteFarmCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.FarmModule.Delete));

        return app;
    }
}
