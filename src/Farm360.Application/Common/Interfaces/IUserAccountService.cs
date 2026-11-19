namespace Farm360.Application.Common.Interfaces;

/// <summary>A user account as seen by tenant member management (no secrets).</summary>
public sealed record UserAccountSummary(
    Guid UserId,
    string? FullName,
    string? PhoneNumber,
    string? Email,
    DateTime? LastLoginAt);

/// <summary>
/// User-account operations owned by the Identity layer (ASP.NET Identity), exposed to the
/// Application layer for member management, invitations and password flows.
/// </summary>
public interface IUserAccountService
{
    Task<IReadOnlyDictionary<Guid, UserAccountSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>The account whose login phone (user name) matches, or null.</summary>
    Task<UserAccountSummary?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    /// <summary>The single account with this email, or null when none or ambiguous.</summary>
    Task<UserAccountSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a password account (phone = login). Throws <see cref="Exceptions.ValidationException"/>
    /// when the password policy or uniqueness rules reject it. Returns the new user id.
    /// </summary>
    Task<Guid> CreateUserAsync(string phone, string? email, string fullName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the current password and sets a new one. A wrong current password counts as a
    /// failed sign-in attempt (lockout settings apply).
    /// </summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the person holds the account's password. A wrong password counts as a failed sign-in
    /// (lockout applies) and throws a validation error on <paramref name="property"/>.
    /// </summary>
    Task VerifyPasswordAsync(Guid userId, string password, string property, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password without the old one (after OTP verification).</summary>
    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
}

/// <summary>Builds links into the web app (base URL from <c>App:PublicBaseUrl</c>).</summary>
public interface IPublicLinkBuilder
{
    string InvitationAcceptLink(string rawToken);
}

/// <summary>
/// Invalidates a user's already-issued access tokens (token-version bump) WITHOUT revoking their
/// refresh tokens, so the client silently refreshes and picks up new role/branch claims.
/// Use <see cref="IRefreshTokenService.RevokeAllSessionsAsync"/> to sign the user out instead.
/// </summary>
public interface IAccessTokenRevoker
{
    Task InvalidateAccessTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
