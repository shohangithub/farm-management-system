using System.Security.Claims;
using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Farm360.Identity.Services;

/// <summary>
/// The full name is stored as a user claim rather than a column: the Identity database is not
/// migrated at startup, so a new Users column would break login on any deployment that missed a
/// manual migration. The UserClaims table already exists everywhere.
/// </summary>
public sealed class UserProfileService(UserManager<ApplicationUser> userManager) : IUserProfileService
{
    public const string FullNameClaimType = "full_name";

    public async Task<UserProfileDetails> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        var claims = await userManager.GetClaimsAsync(user);
        var fullName = claims.FirstOrDefault(c => c.Type == FullNameClaimType)?.Value;

        return new UserProfileDetails(fullName, user.Email, user.PhoneNumber);
    }

    public async Task UpdateAsync(Guid userId, string fullName, string? email, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);

        await SetFullNameAsync(userManager, user, fullName.Trim());

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var result = await userManager.SetEmailAsync(user, normalizedEmail);
            ThrowIfFailed(result);
        }
    }

    internal static async Task SetFullNameAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string fullName)
    {
        var claims = await userManager.GetClaimsAsync(user);
        var existing = claims.FirstOrDefault(c => c.Type == FullNameClaimType);
        var replacement = new Claim(FullNameClaimType, fullName);

        var result = existing is null
            ? await userManager.AddClaimAsync(user, replacement)
            : await userManager.ReplaceClaimAsync(user, existing, replacement);
        ThrowIfFailed(result);
    }

    private async Task<ApplicationUser> FindAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new UnauthorizedAccessException("User not found.");

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
