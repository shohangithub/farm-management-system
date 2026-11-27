using Farm360.Api.Authorization;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Weather;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Weather;

/// <summary>
/// Farm weather: current conditions, 48-hour and 7-day forecast, cattle heat stress (THI) and advice.
/// Same permission as the dashboard; farm access by FarmAccessMiddleware ({farmId}) and the FarmAccess
/// pipeline behaviour. A farm without coordinates answers 200 with <c>needsLocation: true</c>.
/// </summary>
public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/v1/farms/{farmId:guid}/weather")
            .WithTags("Weather")
            .RequireAuthorization()
            .RequirePermission(Farm360.Persistence.Seed.PermissionConstants.DashboardModule.View)
            .MapGet("/", GetFarmWeather)
            .WithName("GetFarmWeather")
            .WithSummary("The farm's weather, cattle heat-stress index and practical advice.");

        return app;
    }

    private static async Task<IResult> GetFarmWeather(
        Guid farmId,
        [FromServices] ISender sender,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (currentUser.AssignedFarmIds is { } assigned && !assigned.Contains(farmId))
            return Results.Problem("You do not have access to this farm.", statusCode: StatusCodes.Status403Forbidden);

        return Results.Ok(await sender.Send(new GetFarmWeatherQuery(farmId), ct));
    }
}
