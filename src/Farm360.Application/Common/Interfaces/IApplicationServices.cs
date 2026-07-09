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
/// Concrete implementation lives in Farm360.Persistence using IDbContextTransaction.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of Work abstraction for transaction management.
/// Used by TransactionBehavior. Never call directly from handlers.
/// </summary>
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
/// F360-AUTH-2026-001: OTP delivery via SMS. Constitution PRD: Phone is primary identity.
/// OTP values are NEVER logged (SensitiveDataAttribute).
/// </summary>
public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendOtpAsync(string phoneNumber, string otpCode, string purpose, CancellationToken cancellationToken = default);
}

/// <summary>
/// AWS S3 blob storage abstraction.
/// Used for animal photos, reports, documents.
/// </summary>
public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string containerName, string fileName, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string containerName, string fileName, TimeSpan expiry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Background job service abstraction (Hangfire backing implementation).
/// Constitution §11 (Logging): Background jobs MUST call SetTenant() explicitly.
/// F360-MTA-2026-001 Golden Rule §7: No implicit tenant context in background workers.
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
/// Group pattern: {tenantId}:{userId} or {tenantId}:all
/// </summary>
public interface INotificationService
{
    Task SendToUserAsync(Guid tenantId, Guid userId, string eventType, object payload, CancellationToken cancellationToken = default);
    Task SendToTenantAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default);
}
