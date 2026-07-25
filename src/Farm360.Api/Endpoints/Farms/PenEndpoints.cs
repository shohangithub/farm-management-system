using Farm360.Application.Farms.Pens.Commands;
using Farm360.Application.Farms.Pens.DTOs;
using Farm360.Application.Farms.Pens.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.Farms;

public static class PenEndpoints
{
    public static void MapPenEndpoints(this IEndpointRouteBuilder app)
    {
        var shedGroup = app.MapGroup("/api/v1/sheds/{shedId:guid}/pens")
            .WithTags("Pens")
            .RequireAuthorization();

        var penGroup = app.MapGroup("/api/v1/pens")
            .WithTags("Pens")
            .RequireAuthorization();

        // GET: /api/v1/sheds/{shedId}/pens
        shedGroup.MapGet("/", async (Guid shedId, ISender sender) =>
        {
            var result = await sender.Send(new GetPensByShedQuery(shedId));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.PenModule.View}");

        // GET: /api/v1/pens/{id}
        penGroup.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPenByIdQuery(id));
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.PenModule.View}");

        // POST: /api/v1/sheds/{shedId}/pens
        shedGroup.MapPost("/", async (Guid shedId, CreatePenRequest request, ISender sender) =>
        {
            var command = new CreatePenCommand(
                shedId,
                request.PenNumber,
                request.PenName,
                request.Capacity,
                request.AnimalGroup,
                request.Notes);

            var id = await sender.Send(command);
            return Results.Created($"/api/v1/pens/{id}", new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.PenModule.Create}");

        // PUT: /api/v1/pens/{id}
        penGroup.MapPut("/{id:guid}", async (Guid id, UpdatePenRequest request, ISender sender) =>
        {
            var command = new UpdatePenCommand(
                id,
                request.PenName,
                request.Capacity,
                request.AnimalGroup,
                request.Notes,
                request.Status);

            await sender.Send(command);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.PenModule.Edit}");

        // DELETE: /api/v1/pens/{id}
        penGroup.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeletePenCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.PenModule.Delete}");
    }
}

public record CreatePenRequest(
    string PenNumber,
    string PenName,
    int Capacity,
    string? AnimalGroup,
    string? Notes);

public record UpdatePenRequest(
    string PenName,
    int Capacity,
    string? AnimalGroup,
    string? Notes,
    int Status);
