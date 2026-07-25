using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Auth.Queries;

namespace Farm360.Application.Common.Interfaces;

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, string SessionId);
public record LoginRequest(string Phone, string Password);
public record RegisterRequest(string Phone, string Email, string Password, string FullName);

/// <summary>
/// Authentication service abstraction.
/// Implemented in the Identity layer to avoid Application taking a dependency on ASP.NET Core Identity.
/// </summary>
public interface IAuthService
{
    Task<LoginResponse> LoginWithPasswordAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RegisterUserAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}
