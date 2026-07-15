namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Current authenticated user context.
/// Resolved from JWT claims per-request.
/// F360-AUTH-2026-001: Claims: sub, tenant_id, role, farms, tv.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>ApplicationUser.Id from JWT sub claim. Null if unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Organization user's TenantId from JWT tenant_id claim.</summary>
    Guid? TenantId { get; }

    /// <summary>User's role from JWT role claim (e.g. "Owner", "FarmManager").</summary>
    string? Role { get; }

    /// <summary>Farm IDs this user has access to. Null = all farms in tenant.</summary>
    IReadOnlyList<Guid>? AssignedFarmIds { get; }

    /// <summary>JWT token version from tv claim (for revocation check).</summary>
    int? TokenVersion { get; }

    /// <summary>Subscription tier from JWT tier claim.</summary>
    string? SubscriptionTier { get; }

    /// <summary>True if the current user is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>True if the current user is a platform system admin (IsSystemUser).</summary>
    bool IsSystemUser { get; }
}

/// <summary>
/// Tenant context resolved per-request by TenantResolutionMiddleware.
/// F360-MTA-2026-001: Tenant resolved from JWT tenant_id claim.
/// Injected into all application services as scoped.
/// </summary>
public interface ITenantService
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    string TenantName { get; }
    string SubscriptionTier { get; }
    string TenantStatus { get; } // Active, GracePeriod, Suspended
    bool IsActive { get; }
    bool IsGracePeriod { get; }

    /// <summary>Called by TenantResolutionMiddleware to set tenant for the request.</summary>
    void SetTenant(Guid tenantId, string slug, string name, string tier, string status);
}

/// <summary>Abstraction for current UTC time. Enables time mocking in tests.</summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

/// <summary>
/// Abstraction for a database transaction.
/// Constitution §2 (Architecture): Application layer MUST NOT reference EF Core.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>Unit of Work abstraction for transaction management.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(ITransaction transaction, CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(ITransaction transaction, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redis-backed distributed cache service.
/// F360-MTA-2026-001: All cache keys MUST be tenant-scoped.
/// Pattern: {tenantId}:{domain}:{entity}:{key}
/// Financial data: NEVER cached.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>
/// Email service abstraction.
/// F360-AUTH-2026-001: Used for email verification and password reset links.
/// </summary>
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendTemplatedAsync(string to, string templateId, object templateData, CancellationToken cancellationToken = default);
}

/// <summary>
/// SMS service abstraction (primary communication channel — Bangladesh context).
/// F360-AUTH-2026-001: OTP delivery via SMS. Phone is primary identity.
/// OTP values are NEVER logged (SensitiveDataAttribute).
/// </summary>
public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendOtpAsync(string phoneNumber, string otpCode, string purpose, CancellationToken cancellationToken = default);
}

/// <summary>AWS S3 blob storage abstraction.</summary>
public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string containerName, string fileName, TimeSpan expiry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Background job service abstraction (Hangfire backing implementation).
/// Constitution §11: Background jobs MUST call SetTenant() explicitly.
/// </summary>
public interface IBackgroundJobService
{
    string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> job);
    string Schedule<T>(System.Linq.Expressions.Expression<Action<T>> job, TimeSpan delay);
    void AddOrUpdateRecurring<T>(string jobId, System.Linq.Expressions.Expression<Action<T>> job, string cronExpression);
    void Delete(string jobId);
}

/// <summary>
/// Real-time notification service (SignalR backing implementation).
/// F360-MTA-2026-001: All SignalR groups are tenant-scoped.
/// </summary>
public interface INotificationService
{
    Task SendToUserAsync(Guid tenantId, Guid userId, string eventType, object payload, CancellationToken cancellationToken = default);
    Task SendToTenantAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default);
}

// ══════════════════════════════════════════════════════════════════════════════
// New interfaces added for Identity + Multi-Tenant Foundation
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Permission evaluation service.
/// Checks whether a user has a specific permission within a tenant.
/// Results cached in Redis (5 min TTL) for performance.
/// F360-AUTH-2026-001 §7 (Permission-Based Authorization).
/// Cache key: {tenantId}:permissions:{userId}
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns true if the user has the specified permission in the given tenant.</summary>
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>Returns all permission codes for the user in the given tenant. Used for JWT generation.</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Invalidates the cached permissions for a user when their role changes.</summary>
    Task InvalidatePermissionCacheAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// JWT token generation and validation service.
/// F360-AUTH-2026-001 §3 (JWT Structure).
/// Claims: sub, tenant_id, role, tv, tier, farms, sys, jti, iat, exp.
/// </summary>
public interface ITokenService
{
    /// <summary>Generates a short-lived access token (15 min) with all claims.</summary>
    Task<TokenResult> GenerateAccessTokenAsync(
        Guid userId,
        Guid tenantId,
        string role,
        int tokenVersion,
        IEnumerable<string> permissions,
        IEnumerable<Guid>? farmIds = null,
        bool isSystemUser = false,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and extracts claims from a JWT. Returns null if invalid.</summary>
    TokenClaimsResult? ValidateToken(string token);
}

/// <summary>Result of JWT token generation.</summary>
public sealed record TokenResult(
    string AccessToken,
    DateTime ExpiresAt,
    string TokenId);

/// <summary>Validated JWT claims.</summary>
public sealed record TokenClaimsResult(
    Guid UserId,
    Guid TenantId,
    string Role,
    int TokenVersion,
    string TokenId,
    DateTime ExpiresAt);

/// <summary>
/// OTP generation and verification service.
/// Redis is the primary store (5 min TTL). DB stores audit record only.
/// F360-AUTH-2026-001 §4 (OTP Authentication).
/// OTP values are NEVER logged.
/// </summary>
public interface IOtpService
{
    /// <summary>Generates a 6-digit OTP, stores in Redis, sends via SMS.</summary>
    Task<string> GenerateAndSendAsync(string phoneNumber, OtpPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>Verifies OTP. Returns true on success, false on invalid/expired. Locks after 3 attempts.</summary>
    Task<bool> VerifyAsync(string phoneNumber, string otpCode, OtpPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a phone number is currently locked out due to too many OTP attempts.</summary>
    Task<bool> IsLockedOutAsync(string phoneNumber, CancellationToken cancellationToken = default);
}

public enum OtpPurpose
{
    Registration = 0,
    Login = 1,
    PasswordReset = 2,
    EmailVerification = 3,
    TwoFactorAuth = 4
}

/// <summary>
/// Refresh token (UserSession) management.
/// F360-AUTH-2026-001 §5 (Session Management, Token Rotation).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Creates a new session (refresh token) for the user. Returns the raw token.</summary>
    Task<string> CreateSessionAsync(
        Guid userId,
        Guid tenantId,
        string? deviceName,
        string? deviceFingerprint,
        string? ipHash,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>Rotates: validates old token, creates new session, revokes old session.</summary>
    Task<RefreshTokenResult> RotateAsync(string refreshToken, string? ipHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes a specific session (logout).</summary>
    Task RevokeSessionAsync(string refreshToken, SessionRevokeReason reason, CancellationToken cancellationToken = default);

    /// <summary>Revokes all active sessions for a user (e.g. password change, account compromise).</summary>
    Task RevokeAllSessionsAsync(Guid userId, SessionRevokeReason reason, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenResult(
    Guid UserId,
    Guid TenantId,
    string NewRefreshToken,
    DateTime ExpiresAt);

public enum SessionRevokeReason
{
    Logout = 0,
    PasswordChange = 1,
    AdminRevoke = 2,
    Suspicious = 3
}

/// <summary>
/// Business audit log writing service.
/// Constitution §11: All entity changes produce an audit record.
/// F360-MTA-2026-001: Audit logs are tenant-scoped and INSERT-only.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        Guid tenantId,
        string entityName,
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        CancellationToken cancellationToken = default);
}
