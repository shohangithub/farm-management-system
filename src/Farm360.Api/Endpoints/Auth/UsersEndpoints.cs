using Farm360.Api.Authorization;
using Farm360.Application.Auth.Commands;
using Farm360.Application.Auth.Queries;
using Farm360.Application.Users.Commands;
using Farm360.Application.Users.Queries;
using Farm360.Domain.Common;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.Auth;

public sealed record ChangeMemberRoleRequest(Guid RoleId);
public sealed record ChangeMemberBranchRequest(Guid? BranchId);

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

        MapTeamEndpoints(group);
        return group;
    }

    /// <summary>
    /// Tenant member management (Team page). Tenant-scoped via the JWT tenant; every route carries
    /// a users.* permission. Handlers add the finer guards (last owner, self lock-out, no granting
    /// above your own permissions).
    /// </summary>
    private static void MapTeamEndpoints(RouteGroupBuilder group)
    {
        // ── Members ──────────────────────────────────────────────────────────
        group.MapGet("/members", async (IMediator mediator) =>
                Results.Ok(await mediator.Send(new GetMembersQuery())))
            .RequirePermission(PermissionConstants.Users.View)
            .WithTags("Team")
            .Produces<IReadOnlyList<MemberDto>>();

        group.MapPut("/members/{memberId:guid}/role", async (Guid memberId, ChangeMemberRoleRequest body, IMediator mediator) =>
            {
                await mediator.Send(new ChangeMemberRoleCommand(memberId, body.RoleId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.AssignRole)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/members/{memberId:guid}/branch", async (Guid memberId, ChangeMemberBranchRequest body, IMediator mediator) =>
            {
                await mediator.Send(new ChangeMemberBranchCommand(memberId, body.BranchId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Edit)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/members/{memberId:guid}/deactivate", async (Guid memberId, IMediator mediator) =>
            {
                await mediator.Send(new DeactivateMemberCommand(memberId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Edit)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/members/{memberId:guid}/reactivate", async (Guid memberId, IMediator mediator) =>
            {
                await mediator.Send(new ReactivateMemberCommand(memberId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Edit)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/members/{memberId:guid}", async (Guid memberId, IMediator mediator) =>
            {
                await mediator.Send(new RemoveMemberCommand(memberId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Remove)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        // ── Lookups for the Team page ────────────────────────────────────────
        group.MapGet("/roles", async (IMediator mediator) =>
                Results.Ok(await mediator.Send(new GetAssignableRolesQuery())))
            .RequirePermission(PermissionConstants.Users.View)
            .WithTags("Team")
            .Produces<IReadOnlyList<RoleOptionDto>>();

        group.MapGet("/branches", async (IMediator mediator) =>
                Results.Ok(await mediator.Send(new GetTeamBranchesQuery())))
            .RequirePermission(PermissionConstants.Users.View)
            .WithTags("Team")
            .Produces<IReadOnlyList<LookupItem>>();

        // ── Invitations ──────────────────────────────────────────────────────
        group.MapGet("/invitations", async (IMediator mediator) =>
                Results.Ok(await mediator.Send(new GetInvitationsQuery())))
            .RequirePermission(PermissionConstants.Users.View)
            .WithTags("Team")
            .Produces<IReadOnlyList<InvitationDto>>();

        group.MapPost("/invitations", async (CreateInvitationCommand command, IMediator mediator) =>
            {
                var id = await mediator.Send(command);
                return Results.Ok(new { id });
            })
            .RequirePermission(PermissionConstants.Users.Invite)
            .WithTags("Team")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/invitations/{invitationId:guid}/resend", async (Guid invitationId, IMediator mediator) =>
            {
                await mediator.Send(new ResendInvitationCommand(invitationId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Invite)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/invitations/{invitationId:guid}/revoke", async (Guid invitationId, IMediator mediator) =>
            {
                await mediator.Send(new RevokeInvitationCommand(invitationId));
                return Results.NoContent();
            })
            .RequirePermission(PermissionConstants.Users.Invite)
            .WithTags("Team")
            .Produces(StatusCodes.Status204NoContent);
    }
}
