using Farm360.Api.Authorization;
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
            .RequireAuthorization()
            .RequirePermission(Farm360.Persistence.Seed.PermissionConstants.AnalyticsModule.View);

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

        group.MapGet("/health/trends", GetHealthTrends)
            .WithName("GetHealthTrends")
            .WithSummary("Gets 12-month health aggregates: deaths and treatments per month, top illnesses, vaccine coverage.")
            .WithTags("Analytics")
            .Produces<HealthTrendsDto>();
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

    private static async Task<IResult> GetHealthTrends(
        Guid farmId,
        [FromServices] ISender sender,
        System.Threading.CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetHealthTrendsQuery(farmId), cancellationToken);
        return Results.Ok(result);
    }
}
