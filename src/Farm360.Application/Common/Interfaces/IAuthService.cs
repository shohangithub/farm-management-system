using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Auth.Queries;

namespace Farm360.Application.Common.Interfaces;

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string SessionId);
public record LoginRequest(string Phone, string Password);
public record RegisterRequest(string Phone, string Email, string Password, string FullName, string? FarmName = null, int? TrialDays = null);

/// <summary>
/// Authentication service abstraction.
/// Implemented in the Identity layer to avoid Application taking a dependency on ASP.NET Core Identity.
/// </summary>
public interface IAuthService
{
    Task<LoginResponse> LoginWithPasswordAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RegisterUserAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a fresh access + refresh token pair for an already-verified user (e.g. right after a
    /// password change revoked every older session), preferring <paramref name="preferredTenantId"/>.
    /// </summary>
    Task<LoginResponse> IssueSessionAsync(Guid userId, Guid? preferredTenantId, CancellationToken cancellationToken = default);
}
