using Farm360.Application.Common.Interfaces;
using Farm360.Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Linq;
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

    /// <summary>
    /// A user restricted to specific farms (<see cref="ICurrentUserService.AssignedFarmIds"/> non-null)
    /// must not be able to pull another farm's dashboard data just by editing the farmId route segment.
    /// Null means "all farms in the tenant" -- the one legitimate unrestricted case.
    /// </summary>
    private static bool IsForbiddenFarm(Guid farmId, ICurrentUserService currentUser) =>
        currentUser.AssignedFarmIds is { } assigned && !assigned.Contains(farmId);

    private static async Task<IResult> GetExecutiveDashboard(
        Guid farmId,
        [FromServices] ISender sender,
        [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser))
        {
            return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        }

        var query = new GetExecutiveDashboardQuery(farmId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetHerdComposition(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetHerdCompositionAsync(farmId));
    }

    private static async Task<IResult> GetAdgTrends(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetAdgTrendsAsync(farmId));
    }

    private static async Task<IResult> GetFeedCostTrends(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetFeedCostTrendsAsync(farmId));
    }

    private static async Task<IResult> GetVaccinationCompliance(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetVaccinationComplianceAsync(farmId));
    }

    private static async Task<IResult> GetFarmSummaryCards(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetFarmSummaryCardsAsync(farmId));
    }

    private static async Task<IResult> GetRecentActivityFeed(Guid farmId, [FromServices] Farm360.Application.Analytics.Queries.IAnalyticsQueryService service, [FromServices] ICurrentUserService currentUser)
    {
        if (IsForbiddenFarm(farmId, currentUser)) return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);
        return Results.Ok(await service.GetRecentActivityFeedAsync(farmId));
    }
}
