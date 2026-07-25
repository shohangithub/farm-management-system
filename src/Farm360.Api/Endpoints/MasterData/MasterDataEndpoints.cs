using Farm360.Application.MasterData.Commands;
using Farm360.Application.MasterData.DTOs;
using Farm360.Application.MasterData.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.MasterData;

public static class MasterDataEndpoints
{
    public static void MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/master-data")
            .WithTags("Master Data")
            .RequireAuthorization();

        // GET: /api/v1/master-data/{type}
        group.MapGet("/{type:int}", async (int type, ISender sender) =>
        {
            var result = await sender.Send(new GetMasterDataByTypeQuery(type));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.MasterDataModule.View}");

        // GET: /api/v1/master-data/entry/{id}
        group.MapGet("/entry/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetMasterDataByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.MasterDataModule.View}");

        // POST: /api/v1/master-data
        group.MapPost("/", async ([FromBody] CreateMasterDataCommand command, ISender sender) =>
        {
            var id = await sender.Send(command);
            return Results.Created($"/api/v1/master-data/entry/{id}", new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.MasterDataModule.Manage}");

        // PUT: /api/v1/master-data/{id}
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateMasterDataCommand command, ISender sender) =>
        {
            if (id != command.Id) return Results.BadRequest("Id mismatch");
            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.MasterDataModule.Manage}");

        // DELETE: /api/v1/master-data/{id}
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteMasterDataCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.MasterDataModule.Manage}");
    }
}
