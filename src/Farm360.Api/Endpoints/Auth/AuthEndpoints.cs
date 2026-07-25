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
        .WithTags("Auth")
        .Produces<LoginResponse>();

        group.MapPost("/refresh", async ([FromBody] RefreshTokenApiRequest request, IAuthService authService) =>
        {
            var result = await authService.RefreshTokenAsync(request.RefreshToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithTags("Auth")
        .Produces<LoginResponse>();

        group.MapPost("/logout", async ([FromBody] LogoutRequest request, IRefreshTokenService refreshTokenService) =>
        {
            await refreshTokenService.RevokeSessionAsync(
                request.RefreshToken,
                SessionRevokeReason.Logout);
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

        // Phase 2: Password Reset via OTP (F360-AUTH-2026-001 §4 — not implemented yet)
        // POST /forgot-password → generate OTP, send via SMS
        // POST /reset-password  → verify OTP, set new password

        return group;
    }
}
