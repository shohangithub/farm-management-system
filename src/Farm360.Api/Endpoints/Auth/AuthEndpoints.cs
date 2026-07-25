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

        group.MapPost("/forgot-password", async ([FromBody] ForgotPasswordRequest request) =>
        {
            // Placeholder for email/OTP logic
            await Task.Delay(100);
            return Results.Ok(new { message = "If the email is registered, a reset link has been sent." });
        })
        .AllowAnonymous()
        .WithTags("Auth")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/reset-password", async ([FromBody] ResetPasswordRequest request) =>
        {
            // Placeholder for password reset logic
            await Task.Delay(100);
            return Results.Ok(new { message = "Password reset successfully." });
        })
        .AllowAnonymous()
        .WithTags("Auth")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}

public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
