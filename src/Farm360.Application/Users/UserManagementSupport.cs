using System.Security.Cryptography;
using System.Text;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using FluentValidation.Results;

namespace Farm360.Application.Users;

/// <summary>Random single-use tokens (invitation links, password reset); only hashes are stored.</summary>
public static class SecureTokens
{
    /// <summary>256-bit URL-safe random token.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Lower-case hex SHA-256 of the raw token.</summary>
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken ?? string.Empty))).ToLowerInvariant();
}

/// <summary>FluentValidation rules mirroring the ASP.NET Identity password options (IdentityServiceExtensions).</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinimumLength).WithMessage($"Password must be at least {MinimumLength} characters.")
            .MaximumLength(128)
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");
}

internal static class Fail
{
    public static Farm360.Application.Common.Exceptions.ValidationException Validation(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}

/// <summary>
/// Plan seat limit (Tenant.MaxUsers): active + pending members + open invitations. Takes the tenant
/// quota lock first so the count and the following insert are serialised per tenant.
/// </summary>
public static class SeatQuota
{
    /// <param name="convertingInvitationId">
    /// When an invitation is being accepted it already holds a seat; exclude it so it is not counted twice.
    /// </param>
    public static async Task EnsureSeatAvailableAsync(
        ITenantRepository tenants,
        ITenantUserRepository members,
        IInvitationRepository invitations,
        Guid tenantId,
        DateTime nowUtc,
        Guid? convertingInvitationId,
        CancellationToken cancellationToken)
    {
        await tenants.AcquireQuotaLockAsync(tenantId, cancellationToken);

        var tenant = await tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw Fail.Validation("TenantId", "Tenant not found.");

        var used = await members.CountSeatHoldersAsync(tenantId, cancellationToken)
                   + await invitations.CountOpenAsync(tenantId, nowUtc, convertingInvitationId, cancellationToken);

        if (used + 1 > tenant.MaxUsers)
        {
            throw Fail.Validation(
                "Users",
                $"Your {tenant.SubscriptionTier} plan allows up to {tenant.MaxUsers} user(s), including pending invitations. " +
                "Upgrade your subscription or revoke a pending invitation.");
        }
    }
}

/// <summary>
/// "A manager cannot grant permissions above their own": the caller must hold every permission of
/// the role being granted, and of the member being managed.
/// </summary>
public static class MemberAuthority
{
    /// <summary>The caller's permission set in the tenant; null = unrestricted (platform admin).</summary>
    public static async Task<HashSet<string>?> CallerPermissionsAsync(
        ICurrentUserService currentUser,
        IPermissionService permissions,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsSystemUser) return null;
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var codes = await permissions.GetPermissionsAsync(userId, tenantId, cancellationToken);
        return new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<bool> CanGrantAsync(
        IRoleRepository roles, HashSet<string>? callerPermissions, Guid roleId, CancellationToken cancellationToken)
    {
        if (callerPermissions is null) return true;
        var roleCodes = await roles.GetPermissionCodesAsync(roleId, cancellationToken);
        return roleCodes.All(callerPermissions.Contains);
    }

    public static async Task EnsureCanGrantAsync(
        IRoleRepository roles, HashSet<string>? callerPermissions, Guid roleId, CancellationToken cancellationToken)
    {
        if (!await CanGrantAsync(roles, callerPermissions, roleId, cancellationToken))
            throw new ForbiddenAccessException("You cannot grant a role that has permissions you do not have.");
    }

    public static async Task EnsureCanManageAsync(
        IRoleRepository roles, HashSet<string>? callerPermissions, TenantUser member, CancellationToken cancellationToken)
    {
        if (!await CanGrantAsync(roles, callerPermissions, member.RoleId, cancellationToken))
            throw new ForbiddenAccessException("You cannot manage a member who has permissions you do not have.");
    }
}

internal static class Contact
{
    public static string? NormalizePhone(string? phone) =>
        Farm360.Application.Common.Phones.PhoneNumberNormalizer.Normalize(phone);

    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public static string? MaskPhone(string? phone) =>
        phone is null ? null : phone.Length < 5 ? "***" : string.Concat(phone.AsSpan(0, 3), "***", phone.AsSpan(phone.Length - 2));

    public static string? MaskEmail(string? email)
    {
        if (email is null) return null;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 1) return "***" + (at >= 0 ? email[at..] : string.Empty);
        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(at));
    }
}
