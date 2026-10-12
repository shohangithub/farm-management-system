using Farm360.Application.Auth.Commands;
using Farm360.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.Auth;

public static class UsersEndpoints
{
    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCurrentUserQuery());
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Users")
        .Produces<UserProfileDto>();

        group.MapPut("/profile", async (UpdateMyProfileCommand command, IMediator mediator) =>
        {
            await mediator.Send(command);
            var result = await mediator.Send(new GetCurrentUserQuery());
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Users")
        .Produces<UserProfileDto>();

        return group;
    }
}
