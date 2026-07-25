using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Identity.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    IPermissionService permissionService) : IAuthService
{
    public async Task<LoginResponse> LoginWithPasswordAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.Phone);
        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new AuthenticationException("Invalid phone number or password.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            throw new AccountLockedException(user.LockoutEnd.Value);
        }

        var role = "Owner"; // TODO: Fetch from OrganizationUser based on resolved TenantId 
        
        var tenantId = Guid.Empty; // Placeholder for tenant resolution later
        
        var permissions = await permissionService.GetPermissionsAsync(user.Id, tenantId, cancellationToken);

        var tokenResult = await tokenService.GenerateAccessTokenAsync(
            userId: user.Id,
            tenantId: tenantId,
            role: role,
            tokenVersion: user.TokenVersion,
            permissions: permissions,
            isSystemUser: user.IsSystemUser,
            cancellationToken: cancellationToken);

        var refreshToken = await refreshTokenService.CreateSessionAsync(
            userId: user.Id,
            tenantId: tenantId,
            deviceName: "Web Client",
            deviceFingerprint: null,
            ipHash: null,
            userAgent: null,
            cancellationToken: cancellationToken);

        return new LoginResponse(
            AccessToken: tokenResult.AccessToken,
            RefreshToken: refreshToken,
            ExpiresIn: (int)(tokenResult.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            SessionId: tokenResult.TokenId);
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var rotationResult = await refreshTokenService.RotateAsync(refreshToken, ipHash: null, cancellationToken);
        
        var user = await userManager.FindByIdAsync(rotationResult.UserId.ToString());
        _ = user ?? throw new AuthenticationException("User not found.");

        var role = "Owner"; // TODO: Fetch from OrganizationUser based on resolved TenantId
        
        var permissions = await permissionService.GetPermissionsAsync(user.Id, rotationResult.TenantId, cancellationToken);

        var tokenResult = await tokenService.GenerateAccessTokenAsync(
            userId: user.Id,
            tenantId: rotationResult.TenantId,
            role: role,
            tokenVersion: user.TokenVersion,
            permissions: permissions,
            isSystemUser: user.IsSystemUser,
            cancellationToken: cancellationToken);

        return new LoginResponse(
            AccessToken: tokenResult.AccessToken,
            RefreshToken: rotationResult.NewRefreshToken,
            ExpiresIn: (int)(tokenResult.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            SessionId: tokenResult.TokenId);
    }

    public async Task RegisterUserAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await userManager.FindByNameAsync(request.Phone);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this phone number already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Phone,
            PhoneNumber = request.Phone,
            Email = request.Email,
            IsSystemUser = false
        };

        var result = await userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Role assignment happens at the Organization/Tenant level in business logic, not here.
    }
}
