using Farm360.Api.Configuration;
using Farm360.Application.Auth.Commands;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Users.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.Auth;

public sealed record InvitationTokenRequest(string Token);

/// <summary>
/// Account self-service under /api/v1/auth: password change (authenticated), OTP password reset and
/// invitation accept (anonymous, auth rate limit). Anonymous responses never reveal whether a phone
/// number has an account (forgot-password always answers 202).
/// </summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        // ── Change password (signed-in user, own account only) ─────────────────
        group.MapPost("/change-password", async ([FromBody] ChangePasswordCommand command, IMediator mediator) =>
                Results.Ok(await mediator.Send(command)))
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces<LoginResponse>();

        // ── Forgot / reset password via SMS OTP ────────────────────────────────
        group.MapPost("/password/forgot", async ([FromBody] RequestPasswordResetCommand command, IMediator mediator) =>
            {
                await mediator.Send(command);
                return Results.Accepted(value: new
                {
                    message = "If an account exists for this phone number, a verification code has been sent.",
                });
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/password/verify", async ([FromBody] VerifyPasswordResetCodeCommand command, IMediator mediator) =>
                Results.Ok(await mediator.Send(command)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces<PasswordResetTokenDto>();

        group.MapPost("/password/reset", async ([FromBody] ResetPasswordCommand command, IMediator mediator) =>
            {
                await mediator.Send(command);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces(StatusCodes.Status204NoContent);

        // ── Invitation accept (token in the body, never in the URL path/logs) ─
        group.MapPost("/invitations/preview", async ([FromBody] InvitationTokenRequest request, IMediator mediator) =>
                Results.Ok(await mediator.Send(new GetInvitationPreviewQuery(request.Token))))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces<InvitationPreviewDto>();

        group.MapPost("/invitations/accept", async ([FromBody] AcceptInvitationCommand command, IMediator mediator) =>
                Results.Ok(await mediator.Send(command)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.AuthPolicy)
            .WithTags("Auth")
            .Produces<AcceptInvitationResult>();

        return group;
    }
}
