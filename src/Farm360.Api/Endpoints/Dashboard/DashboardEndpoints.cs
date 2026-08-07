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
    }

    private static async Task<IResult> GetExecutiveDashboard(
        Guid farmId,
        [FromServices] ISender sender)
    {
        var query = new GetExecutiveDashboardQuery(farmId);
        var result = await sender.Send(query);
        return Results.Ok(result);
    }
}
