using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Farm360.Identity.Services;

/// <summary>
/// JWT access token generation service.
/// F360-AUTH-2026-001 §3 (JWT Structure):
///   - sub:       ApplicationUser.Id
///   - tenant_id: Tenant GUID
///   - role:      Farm360 role name
///   - tv:        Token version (revocation counter)
///   - tier:      SubscriptionTier name
///   - farms:     Comma-separated farm GUIDs (null = all)
///   - sys:       "true" if system user
///   - jti:       Unique token ID (for blacklisting)
///   - iat / exp: Standard claims
/// Token lifetime: 15 minutes (short-lived for security).
/// Signing: HS256 (upgrade to RS256 via JWKS in production).
/// </summary>
public sealed class JwtTokenService(IOptions<JwtConfiguration> jwtOptions) : ITokenService
{
    private readonly JwtConfiguration _jwt = jwtOptions.Value;

    public Task<TokenResult> GenerateAccessTokenAsync(
        Guid userId,
        Guid tenantId,
        string role,
        int tokenVersion,
        IEnumerable<string> permissions,
        IEnumerable<Guid>? farmIds = null,
        bool isSystemUser = false,
        CancellationToken cancellationToken = default)
    {
        var tokenId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_jwt.AccessTokenExpiryMinutes);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role),
            new("tv", tokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("tier", _jwt.DefaultTier),
        };

        if (isSystemUser)
            claims.Add(new Claim("sys", "true"));

        if (farmIds != null)
        {
            var farmList = string.Join(',', farmIds.Select(f => f.ToString()));
            if (!string.IsNullOrEmpty(farmList))
                claims.Add(new Claim("farms", farmList));
        }

        // Embed permission codes in JWT to avoid per-request DB lookups
        // Kept compact: only codes, not descriptions
        var permList = permissions.ToList();
        if (permList.Count > 0)
            claims.Add(new Claim("perms", string.Join(',', permList)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            SigningCredentials = signingCredentials
        };

        var handler = new JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false
        };

        var token = handler.CreateToken(descriptor);

        return Task.FromResult(new TokenResult(token, expiresAt, tokenId));
    }

    public TokenClaimsResult? ValidateToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));

        var result = handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        }).GetAwaiter().GetResult();

        if (!result.IsValid) return null;

        var jwt = result.ClaimsIdentity;

        if (!Guid.TryParse(jwt.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return null;
        if (!Guid.TryParse(jwt.FindFirst("tenant_id")?.Value, out var tenantId)) return null;
        if (!int.TryParse(jwt.FindFirst("tv")?.Value, out var tv)) return null;

        var role = jwt.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var jti = jwt.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? string.Empty;
        var exp = result.SecurityToken.ValidTo;

        return new TokenClaimsResult(userId, tenantId, role, tv, jti, exp);
    }
}
