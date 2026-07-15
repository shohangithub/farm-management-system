namespace Farm360.Domain.Tenancy;

/// <summary>
/// Lifecycle state of a tenant subscription.
/// Constitution §22 (Multi-Tenant): Suspended tenants get read-only access.
/// </summary>
public enum TenantStatus
{
    /// <summary>Active, all features available.</summary>
    Active = 1,

    /// <summary>Payment overdue. Read-only access. Auto-suspends after grace period.</summary>
    GracePeriod = 2,

    /// <summary>Access blocked. Data preserved. Can be re-activated.</summary>
    Suspended = 3,

    /// <summary>Account cancelled. Data retained for 90 days then purged.</summary>
    Cancelled = 4
}

/// <summary>
/// SaaS subscription tier. Determines feature limits and quotas.
/// F360-MTA-2026-001 §4 (Subscription Management).
/// </summary>
public enum SubscriptionTier
{
    /// <summary>Up to 1 farm, 100 animals, 3 users.</summary>
    Starter = 1,

    /// <summary>Up to 5 farms, 500 animals, 10 users.</summary>
    Standard = 2,

    /// <summary>Up to 20 farms, 5 000 animals, 50 users.</summary>
    Professional = 3,

    /// <summary>Unlimited farms, animals, users. Custom SLA.</summary>
    Enterprise = 4
}

/// <summary>Legal/operational type of an Organization.</summary>
public enum OrganizationType
{
    Farm = 1,
    Cooperative = 2,
    Corporation = 3,
    Government = 4,
    Research = 5
}
