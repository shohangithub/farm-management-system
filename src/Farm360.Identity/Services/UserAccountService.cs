using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Phones;
using Farm360.Identity.Context;
using Farm360.Identity.Entities;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Farm360.Identity.Services;

/// <summary>ASP.NET Identity-backed implementation of <see cref="IUserAccountService"/>.</summary>
public sealed class UserAccountService(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext identityContext) : IUserAccountService
{
    public async Task<IReadOnlyDictionary<Guid, UserAccountSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, UserAccountSummary>();

        var ids = userIds.Distinct().ToList();
        var users = await identityContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.PhoneNumber, u.UserName, u.Email, u.LastLoginAt })
            .ToListAsync(cancellationToken);

        var names = await identityContext.UserClaims.AsNoTracking()
            .Where(c => ids.Contains(c.UserId) && c.ClaimType == UserProfileService.FullNameClaimType)
            .Select(c => new { c.UserId, c.ClaimValue })
            .ToListAsync(cancellationToken);
        var nameByUser = names.GroupBy(n => n.UserId).ToDictionary(g => g.Key, g => g.First().ClaimValue);

        return users.ToDictionary(
            u => u.Id,
            u => new UserAccountSummary(u.Id, nameByUser.GetValueOrDefault(u.Id), u.PhoneNumber ?? u.UserName, u.Email, u.LastLoginAt));
    }

    public async Task<UserAccountSummary?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        // Canonical form first, then the value as typed (accounts stored in an older format).
        foreach (var candidate in PhoneNumberNormalizer.LookupCandidates(phone))
        {
            var user = await userManager.FindByNameAsync(candidate);
            if (user is not null) return await SummaryAsync(user.Id, cancellationToken);
        }

        return null;
    }

    public async Task<UserAccountSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = userManager.NormalizeEmail(email.Trim());

        // Emails are not unique in this system (phone is the login): only an unambiguous match counts.
        var matches = await identityContext.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => u.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        return matches.Count == 1 ? await SummaryAsync(matches[0], cancellationToken) : null;
    }

    public async Task<Guid> CreateUserAsync(string phone, string? email, string fullName, string password, CancellationToken cancellationToken = default)
    {
        var canonicalPhone = PhoneNumberNormalizer.Normalize(phone) ?? phone;
        var user = new ApplicationUser
        {
            UserName = canonicalPhone,
            PhoneNumber = canonicalPhone,
            Email = email,
            IsSystemUser = false,
        };

        var result = await userManager.CreateAsync(user, password);
        ThrowIfFailed(result, "Password");

        await UserProfileService.SetFullNameAsync(userManager, user, fullName);
        return user.Id;
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (await userManager.IsLockedOutAsync(user))
            throw new AccountLockedException(user.LockoutEnd!.Value);

        if (!await userManager.CheckPasswordAsync(user, currentPassword))
        {
            // A wrong current password counts towards the normal sign-in lockout.
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
                throw new AccountLockedException(user.LockoutEnd!.Value);
            throw new ValidationException([new ValidationFailure("CurrentPassword", "The current password is incorrect.")]);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        ThrowIfFailed(result, "NewPassword");
    }

    public async Task VerifyPasswordAsync(Guid userId, string password, string property, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new ValidationException([new ValidationFailure(property, "The password is incorrect.")]);

        if (await userManager.IsLockedOutAsync(user))
            throw new AccountLockedException(user.LockoutEnd!.Value);

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
                throw new AccountLockedException(user.LockoutEnd!.Value);
            throw new ValidationException([new ValidationFailure(property, "The password is incorrect.")]);
        }

        await userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new ValidationException([new ValidationFailure("ResetToken", "This password reset session has expired. Please start again.")]);

        // Validate first so a policy failure leaves the old password in place.
        foreach (var validator in userManager.PasswordValidators)
        {
            ThrowIfFailed(await validator.ValidateAsync(userManager, user, newPassword), "NewPassword");
        }

        // Lockout state (LockoutEnd / failed count) is deliberately left as is: a reset does not
        // lift an active sign-in lockout.
        if (await userManager.HasPasswordAsync(user))
        {
            ThrowIfFailed(await userManager.RemovePasswordAsync(user), "NewPassword");
        }

        ThrowIfFailed(await userManager.AddPasswordAsync(user, newPassword), "NewPassword");
    }

    private async Task<UserAccountSummary?> SummaryAsync(Guid userId, CancellationToken cancellationToken) =>
        (await GetSummariesAsync([userId], cancellationToken)).GetValueOrDefault(userId);

    private static void ThrowIfFailed(IdentityResult result, string property)
    {
        if (result.Succeeded) return;
        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(property, e.Description)));
    }
}

/// <summary>Links into the Angular app. Base URL: <c>App:PublicBaseUrl</c> (fallback http://localhost:4200).</summary>
public sealed class PublicLinkBuilder(IConfiguration configuration) : IPublicLinkBuilder
{
    public const string ConfigKey = "App:PublicBaseUrl";

    public string InvitationAcceptLink(string rawToken)
    {
        var baseUrl = configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:4200";
        return $"{baseUrl.TrimEnd('/')}/auth/accept-invitation?token={Uri.EscapeDataString(rawToken)}";
    }
}

/// <summary>Bumps the user's token version only (refresh tokens stay valid → silent refresh picks up new claims).</summary>
public sealed class AccessTokenRevoker(
    IdentityDbContext identityContext,
    ITokenVersionValidator tokenVersionValidator) : IAccessTokenRevoker
{
    public async Task InvalidateAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await identityContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return;

        user.TokenVersion++;
        await identityContext.SaveChangesAsync(cancellationToken);
        tokenVersionValidator.Invalidate(userId);
    }
}
