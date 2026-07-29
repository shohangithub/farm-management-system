using Farm360.Application.Common.Models;
using Farm360.Application.Livestock.Commands;
using Farm360.Application.Livestock.DTOs;
using Farm360.Application.Livestock.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Livestock;

public static class BreedEndpoints
{
    public static RouteGroupBuilder MapBreedEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Breeds");
        group.RequireAuthorization();

        group.MapPost("/breeds", CreateBreed)
            .WithName("CreateBreed")
            .Produces<BreedDto>(201)
            .RequireAuthorization("Permission:masterdata.write");

        group.MapPut("/breeds/{id:guid}", UpdateBreed)
            .WithName("UpdateBreed")
            .Produces<BreedDto>(200)
            .Produces(404)
            .RequireAuthorization("Permission:masterdata.write");

        group.MapDelete("/breeds/{id:guid}", DeleteBreed)
            .WithName("DeleteBreed")
            .Produces(204)
            .Produces(404)
            .RequireAuthorization("Permission:masterdata.write");

        group.MapGet("/breeds", GetBreeds)
            .WithName("GetBreeds")
            .Produces<PagedResult<BreedDto>>()
            .RequireAuthorization("Permission:masterdata.read");

        group.MapGet("/breeds/{id:guid}", GetBreedById)
            .WithName("GetBreedById")
            .Produces<BreedDto>()
            .Produces(404)
            .RequireAuthorization("Permission:masterdata.read");

        return group;
    }

    private static async Task<IResult> CreateBreed(
        [FromBody] CreateBreedCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/v1/livestock/breeds/{result.Id}", result);
    }

    private static async Task<IResult> UpdateBreed(
        Guid id, [FromBody] UpdateBreedRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var command = new UpdateBreedCommand(id, request.Name, request.Description, request.Category, request.Origin, request.MainPurpose, request.BestFor, request.AdgPoorManagement, request.AdgAverageFarm, request.AdgGoodCommercialFarm, request.AdgIntensiveFattening, request.StandardAdgMin, request.StandardAdgMax, request.FcrMin, request.FcrMax, request.MilkYieldMinLiters, request.MilkYieldMaxLiters, request.FatPercentageMin, request.FatPercentageMax);
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteBreed(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteBreedCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetBreeds(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? mainPurpose = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        ISender sender = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetBreedListQuery(pageNumber, pageSize, search, category, mainPurpose, sortBy, sortDesc), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBreedById(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBreedByIdQuery(id), cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}

public sealed record UpdateBreedRequest(
    string Name,
    string Description,
    string Category,
    string Origin,
    string MainPurpose,
    string BestFor,
    decimal AdgPoorManagement,
    decimal AdgAverageFarm,
    decimal AdgGoodCommercialFarm,
    decimal AdgIntensiveFattening,
    decimal StandardAdgMin,
    decimal StandardAdgMax,
    decimal FcrMin,
    decimal FcrMax,
    decimal MilkYieldMinLiters,
    decimal MilkYieldMaxLiters,
    decimal FatPercentageMin,
    decimal FatPercentageMax);
