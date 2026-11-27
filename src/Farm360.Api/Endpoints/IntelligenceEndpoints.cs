using Farm360.Api.Authorization;
using Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;
using Farm360.Application.Intelligence.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace Farm360.Api.Endpoints;

public static class IntelligenceEndpoints
{
    public static void MapIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/intelligence")
            .RequireAuthorization()
            .RequirePermission(Farm360.Persistence.Seed.PermissionConstants.IntelligenceModule.View);

        group.MapGet("/animals/{animalId:guid}/financial-snapshot", GetAnimalFinancialSnapshot)
            .WithName("GetAnimalFinancialSnapshot")
            .WithSummary("Gets the real-time financial snapshot and projections for an animal.")
            .WithTags("Intelligence");

        group.MapGet("/animals/{animalId:guid}/data", GetAnimalIntelligenceData)
            .WithName("GetAnimalIntelligenceData")
            .WithSummary("Gets actionable insights and growth predictions for an animal.")
            .WithTags("Intelligence");
            
        group.MapGet("/animals/{animalId:guid}/simulate-sale", SimulateSale)
            .WithName("SimulateSale")
            .WithSummary("Projects weight and costs to a future target date.")
            .WithTags("Intelligence");

        var projectionGroup = app.MapGroup("/api/v1/intelligence/projections")
            .RequireAuthorization()
            .RequirePermission(Farm360.Persistence.Seed.PermissionConstants.IntelligenceModule.View);

        // The animal page's price and cost-to-date. Projections themselves come from the Sell-or-keep
        // engine: GET /livestock/animals/{id}/fattening-plan.
        projectionGroup.MapGet("/defaults/{animalId:guid}", GetProjectionDefaults)
            .WithName("GetProjectionDefaults")
            .WithSummary("Gets the default projection inputs for a specific animal based on breed and farm settings.")
            .WithTags("Intelligence");
    }

    private static async Task<IResult> GetProjectionDefaults(
        Guid animalId,
        [FromServices] ISender sender)
    {
        var query = new Farm360.Application.Intelligence.Queries.GetProjectionDefaults.GetProjectionDefaultsQuery(animalId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAnimalFinancialSnapshot(
        Guid animalId,
        [FromServices] ISender sender)
    {
        var query = new GetAnimalFinancialSnapshotQuery(animalId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAnimalIntelligenceData(
        Guid animalId,
        [FromServices] ISender sender)
    {
        var query = new GetAnimalIntelligenceDataQuery(animalId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> SimulateSale(
        Guid animalId,
        [FromQuery] DateTime targetDate,
        [FromServices] ISender sender)
    {
        var query = new SimulateSaleQuery(animalId, DateOnly.FromDateTime(targetDate));
        var result = await sender.Send(query);
        return result is not null ? Results.Ok(result) : Results.BadRequest("Simulation failed. Check if animal has enough weight data.");
    }
}
