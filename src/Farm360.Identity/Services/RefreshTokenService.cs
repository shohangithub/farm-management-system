using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Farm360.Identity.Services;

/// <summary>
/// Refresh token (UserSession) management.
/// F360-AUTH-2026-001 §5 (Session Management):
///   - Raw refresh token: 256-bit random bytes (URL-safe Base64)
///   - Stored as HMAC-SHA256 hash — never store raw tokens
///   - Token rotation: old session revoked, new session created, linked via ReplacedBySessionId
///   - Max sessions per user: 5 (oldest revoked on overflow)
/// </summary>
public sealed class RefreshTokenService(
    IdentityDbContext identityContext,
    ILogger<RefreshTokenService> logger)
    : IRefreshTokenService
{
    private const int RefreshTokenExpiryDays = 30;
    private const int MaxSessionsPerUser = 5;

    public async Task<string> CreateSessionAsync(
        Guid userId,
        Guid tenantId,
        string? deviceName,
        string? deviceFingerprint,
        string? ipHash,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Enforce max sessions — revoke oldest if over limit
        await EnforceSessionLimitAsync(userId, cancellationToken);

        var rawToken = GenerateRawToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenHash = tokenHash,
            DeviceName = deviceName,
            DeviceFingerprint = deviceFingerprint,
            IpHash = ipHash,
            UserAgent = userAgent,
            IssuedAt = now,
            ExpiresAt = now.AddDays(RefreshTokenExpiryDays),
            LastUsedAt = now,
            IsRevoked = false
        };

        identityContext.UserSessions.Add(session);
        await identityContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    public async Task<RefreshTokenResult> RotateAsync(
        string refreshToken,
        string? ipHash,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var now = DateTime.UtcNow;

        var session = await identityContext.UserSessions
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && !s.IsRevoked, cancellationToken);

        if (session is null)
        {
            logger.LogWarning("Farm360 RefreshToken: Rotation attempted with unknown/revoked token. Hash={Hash}", tokenHash[..8]);
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        if (session.ExpiresAt < now)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = (byte)SessionRevokeReason.Logout;
            await identityContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        // Create new session
        var newRawToken = GenerateRawToken();
        var newTokenHash = HashToken(newRawToken);
        var newExpiresAt = now.AddDays(RefreshTokenExpiryDays);

        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = session.UserId,
            TenantId = session.TenantId,
            TokenHash = newTokenHash,
            DeviceName = session.DeviceName,
            DeviceFingerprint = session.DeviceFingerprint,
            IpHash = ipHash ?? session.IpHash,
            UserAgent = session.UserAgent,
            IssuedAt = now,
            ExpiresAt = newExpiresAt,
            LastUsedAt = now,
            IsRevoked = false
        };

        // Revoke old session
        session.IsRevoked = true;
        session.RevokedAt = now;
        session.RevokedReason = (byte)SessionRevokeReason.Logout; // Rotation = soft revoke
        session.ReplacedBySessionId = newSession.Id;
        session.LastUsedAt = now;

        identityContext.UserSessions.Add(newSession);
        await identityContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResult(session.UserId, session.TenantId, newRawToken, newExpiresAt);
    }

    public async Task RevokeSessionAsync(
        string refreshToken,
        SessionRevokeReason reason,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var session = await identityContext.UserSessions
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && !s.IsRevoked, cancellationToken);

        if (session is null) return;

        session.IsRevoked = true;
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = (byte)reason;
        await identityContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        SessionRevokeReason reason,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeSessions = await identityContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = (byte)reason;
        }

        await identityContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Farm360 RefreshToken: Revoked {Count} sessions for User={UserId} Reason={Reason}",
            activeSessions.Count, userId, reason);
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private static string GenerateRawToken()
    {
        var bytes = new byte[32]; // 256 bits
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task EnforceSessionLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeSessions = await identityContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .OrderBy(s => s.IssuedAt)
            .ToListAsync(cancellationToken);

        if (activeSessions.Count < MaxSessionsPerUser) return;

        // Revoke oldest sessions to stay under limit
        var toRevoke = activeSessions.Take(activeSessions.Count - MaxSessionsPerUser + 1);
        foreach (var session in toRevoke)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedReason = (byte)SessionRevokeReason.AdminRevoke;
        }
    }
}
