using Farm360.Application.MasterData.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.MasterData;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/locations")
            .WithTags("Locations")
            .RequireAuthorization();

        group.MapGet("/countries", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCountriesQuery());
            return Results.Ok(result);
        });

        group.MapGet("/divisions", async ([FromQuery] Guid countryId, ISender sender) =>
        {
            var result = await sender.Send(new GetDivisionsQuery(countryId));
            return Results.Ok(result);
        });

        group.MapGet("/districts", async ([FromQuery] Guid divisionId, ISender sender) =>
        {
            var result = await sender.Send(new GetDistrictsQuery(divisionId));
            return Results.Ok(result);
        });

        group.MapGet("/upazilas", async ([FromQuery] Guid districtId, ISender sender) =>
        {
            var result = await sender.Send(new GetUpazilasQuery(districtId));
            return Results.Ok(result);
        });

        group.MapGet("/unions", async ([FromQuery] Guid upazilaId, ISender sender) =>
        {
            var result = await sender.Send(new GetUnionsQuery(upazilaId));
            return Results.Ok(result);
        });

        group.MapGet("/villages", async ([FromQuery] Guid unionId, ISender sender) =>
        {
            var result = await sender.Send(new GetVillagesQuery(unionId));
            return Results.Ok(result);
        });
    }
}
