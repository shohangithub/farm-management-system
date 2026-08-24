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
            .RequireAuthorization(); // Requires standard authenticated user

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
            .RequireAuthorization();

        projectionGroup.MapPost("/calculate", CalculateProfitProjection)
            .WithName("CalculateProfitProjection")
            .WithSummary("Calculates detailed profit and loss projections for an animal over a fattening period.")
            .WithTags("Intelligence");

        projectionGroup.MapPost("/solve-break-even", SolveBreakEven)
            .WithName("SolveBreakEven")
            .WithSummary("Solves for break-even targets based on projection inputs.")
            .WithTags("Intelligence");

        projectionGroup.MapGet("/defaults/{animalId:guid}", GetProjectionDefaults)
            .WithName("GetProjectionDefaults")
            .WithSummary("Gets the default projection inputs for a specific animal based on breed and farm settings.")
            .WithTags("Intelligence");

        projectionGroup.MapPost("/scenarios", SaveProjectionScenario)
            .WithName("SaveProjectionScenario")
            .WithSummary("Saves a custom projection scenario for future comparison.")
            .WithTags("Intelligence");
    }

    private static async Task<IResult> SaveProjectionScenario(
        [FromBody] Farm360.Application.Intelligence.Commands.SaveProjectionScenario.SaveProjectionScenarioCommand command,
        [FromServices] ISender sender)
    {
        var result = await sender.Send(command);
        return Results.Ok(result);
    }

    private static async Task<IResult> CalculateProfitProjection(
        [FromBody] Farm360.Application.Intelligence.Queries.CalculateProfitProjection.CalculateProfitProjectionQuery query,
        [FromServices] ISender sender)
    {
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> SolveBreakEven(
        [FromBody] Farm360.Application.Intelligence.Queries.SolveBreakEven.SolveBreakEvenQuery query,
        [FromServices] ISender sender)
    {
        var result = await sender.Send(query);
        return Results.Ok(result);
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
