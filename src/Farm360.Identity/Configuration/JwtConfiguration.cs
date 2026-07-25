namespace Farm360.Identity.Configuration;

/// <summary>
/// JWT configuration loaded from appsettings.json → "Jwt" section.
/// F360-AUTH-2026-001 §3.
/// </summary>
public sealed class JwtConfiguration
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing secret. Min 32 chars. Set via environment variable in production.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>JWT issuer (e.g. "https://api.farm360.io").</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>JWT audience (e.g. "https://app.farm360.io").</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Access token lifetime in minutes. Default: 15.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 15;

    /// <summary>Refresh token lifetime in days. Default: 30.</summary>
    public int RefreshTokenExpiryDays { get; init; } = 30;

    /// <summary>Device token lifetime in days. Default: 90.</summary>
    public int DeviceTokenExpiryDays { get; init; } = 90;

    /// <summary>Default subscription tier to embed in JWT when not overridden.</summary>
    public string DefaultTier { get; init; } = "Starter";
}
