using Farm360.Application.Common.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Farm360.Identity.Services;

/// <summary>
/// Resolves current user from JWT claims.
/// F360-AUTH-2026-001 §12.2: Claims: sub, tenant_id, role, farms, tv, tier.
/// Scoped lifetime — one instance per HTTP request.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;

    public Guid? TenantId =>
        Guid.TryParse(Principal?.FindFirstValue("tenant_id"), out var id)
            ? id : null;

    public string? Role =>
        Principal?.FindFirstValue(ClaimTypes.Role);

    public IReadOnlyList<Guid>? AssignedFarmIds
    {
        get
        {
            var farms = Principal?.FindFirstValue("farms");
            if (string.IsNullOrEmpty(farms)) return null;

            return farms.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Guid.TryParse(f.Trim(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList()
                .AsReadOnly();
        }
    }

    public int? TokenVersion =>
        int.TryParse(Principal?.FindFirstValue("tv"), out var tv) ? tv : null;

    public string? SubscriptionTier =>
        Principal?.FindFirstValue("tier");

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsSystemUser =>
        Principal?.FindFirstValue("sys") == "true";
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
