using Farm360.Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace Farm360.Api.Endpoints.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/farms/{farmId:guid}/dashboard")
            .RequireAuthorization();

        group.MapGet("/", GetExecutiveDashboard)
            .WithName("GetExecutiveDashboard")
            .WithSummary("Gets aggregated data for the executive dashboard.")
            .WithTags("Dashboard");

        group.MapGet("/herd-composition", GetHerdComposition).WithName("GetHerdComposition").WithTags("Dashboard");
        group.MapGet("/adg-trends", GetAdgTrends).WithName("GetAdgTrends").WithTags("Dashboard");
        group.MapGet("/feed-cost-trends", GetFeedCostTrends).WithName("GetFeedCostTrends").WithTags("Dashboard");
        group.MapGet("/vaccination-compliance", GetVaccinationCompliance).WithName("GetVaccinationCompliance").WithTags("Dashboard");
        group.MapGet("/farm-summary-cards", GetFarmSummaryCards).WithName("GetFarmSummaryCards").WithTags("Dashboard");
        group.MapGet("/recent-activity", GetRecentActivityFeed).WithName("GetRecentActivityFeed").WithTags("Dashboard");
    }

    private static async Task<IResult> GetExecutiveDashboard(
        Guid farmId,
        [FromServices] ISender sender)
    {
        var query = new GetExecutiveDashboardQuery(farmId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetHerdComposition(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetHerdCompositionAsync(farmId));
    }

    private static async Task<IResult> GetAdgTrends(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetAdgTrendsAsync(farmId));
    }

    private static async Task<IResult> GetFeedCostTrends(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetFeedCostTrendsAsync(farmId));
    }

    private static async Task<IResult> GetVaccinationCompliance(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetVaccinationComplianceAsync(farmId));
    }

    private static async Task<IResult> GetFarmSummaryCards(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetFarmSummaryCardsAsync());
    }

    private static async Task<IResult> GetRecentActivityFeed(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service)
    {
        return Results.Ok(await service.GetRecentActivityFeedAsync(farmId));
    }
}
