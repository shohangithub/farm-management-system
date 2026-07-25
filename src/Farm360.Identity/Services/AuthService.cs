using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace Farm360.Identity.Services;

/// <summary>
/// Authentication service — password login, token refresh, registration.
/// F360-AUTH-2026-001 §2 (Authentication Flow).
///
/// Tenant/role resolution is delegated to ITenantMembershipService (implemented in Farm360.Persistence)
/// to avoid a direct Farm360.Identity → Farm360.Persistence project reference (wrong layer direction).
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    IPermissionService permissionService,
    ITenantMembershipService tenantMembershipService) : IAuthService
{
    public async Task<LoginResponse> LoginWithPasswordAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Find user by phone number (UserName == PhoneNumber in this system)
        var user = await userManager.FindByNameAsync(request.Phone);

        // F360-AUTH-2026-001 §8: Check lockout BEFORE password validation to prevent timing side-channel
        if (user != null && await userManager.IsLockedOutAsync(user))
        {
            throw new AccountLockedException(user.LockoutEnd!.Value);
        }

        // Generic error — never distinguish "user not found" vs "wrong password" (prevents user enumeration)
        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password ?? string.Empty))
        {
            if (user != null)
            {
                // H3: Increment failed access count → triggers lockout after MaxFailedAccessAttempts (5)
                await userManager.AccessFailedAsync(user);
                if (await userManager.IsLockedOutAsync(user))
                    throw new AccountLockedException(user.LockoutEnd!.Value);
            }
            throw new AuthenticationException("Invalid phone number or password.");
        }

        // H3: Reset lockout counter on successful authentication
        await userManager.ResetAccessFailedCountAsync(user);

        // H4: Update audit timestamps
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // C1: Resolve tenant and role from TenantUser (first active membership)
        // System users: bypass tenant lookup → PlatformAdmin on Guid.Empty tenant
        var (tenantId, role) = await ResolveTenantContextAsync(user, preferredTenantId: null, cancellationToken);

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

        // C2: Resolve tenant and role — prefer the tenant stored in the rotated session
        var (tenantId, role) = await ResolveTenantContextAsync(user, preferredTenantId: rotationResult.TenantId, cancellationToken);

        var permissions = await permissionService.GetPermissionsAsync(user.Id, tenantId, cancellationToken);

        var tokenResult = await tokenService.GenerateAccessTokenAsync(
            userId: user.Id,
            tenantId: tenantId,
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
            // Generic error to avoid user enumeration
            throw new InvalidOperationException("Registration failed. Please check your details and try again.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Phone,
            PhoneNumber = request.Phone,
            Email = request.Email,
            IsSystemUser = false
        };

        var result = await userManager.CreateAsync(user, request.Password ?? string.Empty);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Role and tenant assignment happen at the Organization/Tenant level in business logic, not here.
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the TenantId and Role name to embed in the JWT.
    ///
    /// Resolution strategy (per F360-MTA-2026-001 §3):
    ///   1. System users → (Guid.Empty, "PlatformAdmin") — bypasses tenant isolation
    ///   2. preferredTenantId provided → look for active membership in that specific tenant
    ///   3. Fallback → first active TenantUser membership (ordered by JoinedAt)
    ///   4. No membership found → (Guid.Empty, "Viewer") — minimal privileges (valid pre-org state)
    /// </summary>
    private async Task<(Guid TenantId, string Role)> ResolveTenantContextAsync(
        ApplicationUser user,
        Guid? preferredTenantId,
        CancellationToken cancellationToken)
    {
        if (user.IsSystemUser)
            return (Guid.Empty, "PlatformAdmin");

        var membership = await tenantMembershipService.GetActiveMembershipAsync(
            user.Id, preferredTenantId, cancellationToken);

        if (membership == null)
            return (Guid.Empty, "Viewer");

        return (membership.TenantId, membership.RoleName);
    }
}
