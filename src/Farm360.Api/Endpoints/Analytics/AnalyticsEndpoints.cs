using Farm360.Application.Analytics.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace Farm360.Api.Endpoints.Analytics;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/farms/{farmId:guid}/analytics")
            .RequireAuthorization();

        group.MapGet("/breeding", GetBreedingAnalytics)
            .WithName("GetBreedingAnalytics")
            .WithSummary("Gets breeding analytics.")
            .WithTags("Analytics");

        group.MapGet("/finance", GetFinanceAnalytics)
            .WithName("GetFinanceAnalytics")
            .WithSummary("Gets finance and sales analytics.")
            .WithTags("Analytics");

        group.MapGet("/health", GetHealthAnalytics)
            .WithName("GetHealthAnalytics")
            .WithSummary("Gets health and mortality analytics.")
            .WithTags("Analytics");
    }

    private static async Task<IResult> GetBreedingAnalytics(
        Guid farmId,
        [FromServices] ISender sender)
    {
        var query = new GetBreedingAnalyticsQuery(farmId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFinanceAnalytics(
        Guid farmId,
        [FromQuery] int year,
        [FromServices] ISender sender)
    {
        var query = new GetFinanceAnalyticsQuery(farmId, year == 0 ? DateTime.UtcNow.Year : year);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetHealthAnalytics(
        Guid farmId,
        [FromServices] ISender sender)
    {
        var query = new GetHealthAnalyticsQuery(farmId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }
}
