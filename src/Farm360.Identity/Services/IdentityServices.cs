using Farm360.Application.Common.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Farm360.Identity.Services;

/// <summary>
/// Resolves current user from JWT claims.
/// F360-AUTH-2026-001 §12.2: Claims: sub, tenant_id, role, farms, tv, tier.
/// Scoped lifetime — one instance per HTTP request.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, CurrentUserOverride? userOverride = null) : ICurrentUserService
{
    /// <summary>Background work acting as a tenant user (see <see cref="ICurrentUserOverride"/>) wins over the request.</summary>
    private CurrentUserOverride? Override => userOverride is { IsSet: true } ? userOverride : null;

    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Override is { } o ? o.UserId :
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;

    public Guid? TenantId => Override is { } o ? o.TenantId :
        Guid.TryParse(Principal?.FindFirstValue("tenant_id"), out var id)
            ? id : null;

    public string? Role => Override is { } o ? o.Role :
        Principal?.FindFirstValue(ClaimTypes.Role);

    public IReadOnlyList<Guid>? AssignedFarmIds
    {
        get
        {
            if (Override is not null) return null; // acting as the tenant owner: every farm

            var farms = Principal?.FindFirstValue("farms");
            if (string.IsNullOrEmpty(farms)) return null;

            return farms.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Guid.TryParse(f.Trim(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList()
                .AsReadOnly();
        }
    }

    public int? TokenVersion => Override is not null ? null :
        int.TryParse(Principal?.FindFirstValue("tv"), out var tv) ? tv : null;

    public string? SubscriptionTier => Override is { } o ? o.SubscriptionTier :
        Principal?.FindFirstValue("tier");

    public bool IsAuthenticated => Override is not null ||
        (Principal?.Identity?.IsAuthenticated ?? false);

    // Never true for an override: background work acts as a tenant user, not a platform admin.
    public bool IsSystemUser => Override is null &&
        Principal?.FindFirstValue("sys") == "true";
}

/// <inheritdoc cref="ICurrentUserOverride"/>
public sealed class CurrentUserOverride : ICurrentUserOverride
{
    public bool IsSet { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string? SubscriptionTier { get; private set; }

    public void Set(Guid userId, Guid tenantId, string role, string? subscriptionTier = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        UserId = userId;
        TenantId = tenantId;
        Role = role;
        SubscriptionTier = subscriptionTier;
        IsSet = true;
    }
}

/// <summary>
/// Tenant context for the current request.
/// Set by TenantResolutionMiddleware after JWT validation.
/// Scoped lifetime — one instance per HTTP request.
/// </summary>
public sealed class TenantService : ITenantService
{
    private Guid _tenantId;
    private string _tenantSlug = string.Empty;
    private string _tenantName = string.Empty;
    private string _subscriptionTier = string.Empty;
    private string _tenantStatus = string.Empty;

    public Guid TenantId => _tenantId;
    public string TenantSlug => _tenantSlug;
    public string TenantName => _tenantName;
    public string SubscriptionTier => _subscriptionTier;
    public string TenantStatus => _tenantStatus;
    public bool IsActive => _tenantStatus == "Active";
    public bool IsGracePeriod => _tenantStatus == "GracePeriod";

    public void SetTenant(Guid tenantId, string slug, string name, string tier, string status)
    {
        _tenantId = tenantId;
        _tenantSlug = slug;
        _tenantName = name;
        _subscriptionTier = tier;
        _tenantStatus = status;
    }
}

/// <summary>System clock abstraction for testability.</summary>
public sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
