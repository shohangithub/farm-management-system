using Farm360.Application.Auth.Queries;
using Farm360.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Endpoints.Auth;

public record LogoutRequest(string RefreshToken);
public record RefreshTokenApiRequest(string RefreshToken);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async ([FromBody] LoginRequest request, IAuthService authService) =>
        {
            var result = await authService.LoginWithPasswordAsync(request);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting(Farm360.Api.Configuration.RateLimitingSetup.AuthPolicy)
        .WithTags("Auth")
        .Produces<LoginResponse>();

        group.MapPost("/refresh", async ([FromBody] RefreshTokenApiRequest request, IAuthService authService) =>
        {
            var result = await authService.RefreshTokenAsync(request.RefreshToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting(Farm360.Api.Configuration.RateLimitingSetup.AuthPolicy)
        .WithTags("Auth")
        .Produces<LoginResponse>();

        // Revokes only the caller's own refresh token: a token belonging to another user is ignored.
        group.MapPost("/logout", async (
            [FromBody] LogoutRequest request,
            IRefreshTokenService refreshTokenService,
            ICurrentUserService currentUser) =>
        {
            if (currentUser.UserId is not { } userId)
            {
                return Results.Unauthorized();
            }

            await refreshTokenService.RevokeOwnSessionAsync(
                userId,
                request.RefreshToken,
                SessionRevokeReason.Logout);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithTags("Auth")
        .Produces(StatusCodes.Status204NoContent);

        // Signs the caller out everywhere: revokes all refresh tokens and bumps the token version so
        // every access token already issued stops working (see TokenVersionCheck).
        group.MapPost("/logout-all", async (IRefreshTokenService refreshTokenService, ICurrentUserService currentUser) =>
        {
            if (currentUser.UserId is not { } userId)
            {
                return Results.Unauthorized();
            }

            await refreshTokenService.RevokeAllSessionsAsync(userId, SessionRevokeReason.Logout);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithTags("Auth")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/register", async ([FromBody] RegisterRequest request, IAuthService authService) =>
        {
            await authService.RegisterUserAsync(request);
            return Results.Ok(new { message = "User registered successfully" });
        })
        .AllowAnonymous()
        .RequireRateLimiting(Farm360.Api.Configuration.RateLimitingSetup.RegistrationPolicy)
        .WithTags("Auth")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/me", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCurrentUserQuery());
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("Auth")
        .Produces<UserProfileDto>();

        // Password change / OTP password reset / invitation accept (AccountEndpoints.cs)
        group.MapAccountEndpoints();

        return group;
    }
}
