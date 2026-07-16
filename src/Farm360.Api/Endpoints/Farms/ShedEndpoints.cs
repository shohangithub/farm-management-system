using Farm360.Application.Farms.Sheds.Commands;
using Farm360.Application.Farms.Sheds.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Farms;

public static class ShedEndpoints
{
    public static IEndpointRouteBuilder MapShedEndpoints(this IEndpointRouteBuilder app)
    {
        var farmGroup = app.MapGroup("/api/v1/farms/{farmId:guid}/sheds")
            .WithTags("Sheds")
            .RequireAuthorization();

        var rootGroup = app.MapGroup("/api/v1/sheds")
            .WithTags("Sheds")
            .RequireAuthorization();

        farmGroup.MapGet("/", async ([FromRoute] Guid farmId, ISender sender) =>
        {
            var result = await sender.Send(new GetShedsByFarmQuery(farmId));
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.ShedModule.View));

        rootGroup.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetShedByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.ShedModule.View));

        farmGroup.MapPost("/", async ([FromRoute] Guid farmId, [FromBody] CreateShedCommand command, ISender sender) =>
        {
            if (farmId != command.FarmId)
            {
                return Results.BadRequest("FarmId in route does not match command.");
            }

            var id = await sender.Send(command);
            return Results.Created($"/api/v1/sheds/{id}", new { Id = id });
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.ShedModule.Create));

        rootGroup.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateShedCommand command, ISender sender) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest("Id in route does not match command.");
            }

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.ShedModule.Edit));

        rootGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteShedCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireClaim("Permission", PermissionConstants.ShedModule.Delete));

        return app;
    }
}
