using Farm360.Domain.Common;
using Farm360.Domain.Organizations;

namespace Farm360.Domain.Tenancy;

/// <summary>
/// Tenant Aggregate Root — top-level SaaS account (e.g. "Greenfield Farms Ltd").
/// F360-MTA-2026-001: All business data is partitioned by TenantId.
/// Tenant itself is NOT partitioned — it IS the partition boundary.
/// Constitution §22: Status transitions enforced by domain methods (no public setters).
/// </summary>
public sealed class Tenant : BaseEntity
{
    // ── Private constructor (use factory methods) ────────────────────────────
    private Tenant() { }

    private Tenant(Guid id, string name, string slug, SubscriptionTier tier) : base(id)
    {
        Name = name;
        Slug = slug;
        SubscriptionTier = tier;
        Status = TenantStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    // ── Core identity ────────────────────────────────────────────────────────
    /// <summary>Display name of the tenant (e.g. "Greenfield Farms").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// URL-safe unique identifier (e.g. "greenfield-farms").
    /// Used in subdomain routing: greenfield-farms.farm360.io
    /// Immutable after creation.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    // ── Branding ─────────────────────────────────────────────────────────────
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }  // hex: "#1A7F4B"
    public string? TimeZone { get; private set; }      // IANA: "Asia/Dhaka"
    public string? DefaultCurrency { get; private set; } = "BDT";

    // ── Subscription ─────────────────────────────────────────────────────────
    public SubscriptionTier SubscriptionTier { get; private set; }
    public SubscriptionBillingCycle BillingCycle { get; private set; } = SubscriptionBillingCycle.Monthly;
    public TenantStatus Status { get; private set; }
    public DateTime? SubscriptionExpiresAt { get; private set; }
    public DateTime? GracePeriodEndsAt { get; private set; }

    /// <summary>Whether the tenant is currently on a free trial.</summary>
    public bool IsTrial { get; private set; }

    /// <summary>Configured trial duration in days (3, 7, 10).</summary>
    public int? TrialDays { get; private set; }

    /// <summary>When the trial period ends in UTC.</summary>
    public DateTime? TrialEndsAtUtc { get; private set; }

    /// <summary>Tracks whether this tenant has already consumed their free trial.</summary>
    public bool HasUsedTrial { get; private set; }

    // ── Quotas (enforced at Application layer) ─────────────────────────────
    public int MaxUsers { get; private set; } = 3;
    public int MaxFarms { get; private set; } = 1;
    public int MaxAnimals { get; private set; } = 100;

    // ── Audit ─────────────────────────────────────────────────────────────────
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    // ── Collections ──────────────────────────────────────────────────────────
    private readonly List<Organization> _organizations = [];
    public IReadOnlyCollection<Organization> Organizations => _organizations.AsReadOnly();

    // ── Factory ──────────────────────────────────────────────────────────────
    /// <summary>Creates a new Tenant. Validates slug uniqueness must be done at Application layer.</summary>
    public static Tenant Create(string name, string slug, SubscriptionTier tier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var tenant = new Tenant(Guid.NewGuid(), name.Trim(), slug.ToLowerInvariant().Trim(), tier);
        tenant.SetQuotasForTier(tier);

        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Slug));
        return tenant;
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void Activate()
    {
        Status = TenantStatus.Active;
        GracePeriodEndsAt = null;
        Touch();
    }

    public void EnterGracePeriod(DateTime gracePeriodEndsAt)
    {
        if (Status == TenantStatus.Cancelled)
            throw new InvalidOperationException("Cannot enter grace period from Cancelled status.");
        Status = TenantStatus.GracePeriod;
        GracePeriodEndsAt = gracePeriodEndsAt;
        Touch();
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Cancelled)
            throw new InvalidOperationException("Cancelled tenants cannot be suspended.");
        Status = TenantStatus.Suspended;
        Touch();
        RaiseDomainEvent(new TenantSuspendedEvent(Id));
    }

    public void Cancel()
    {
        Status = TenantStatus.Cancelled;
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Upgrade(SubscriptionTier newTier, DateTime expiresAt)
    {
        SubscriptionTier = newTier;
        SubscriptionExpiresAt = expiresAt;
        SetQuotasForTier(newTier);
        Status = TenantStatus.Active;
        Touch();
    }

    /// <summary>
    /// Starts a free trial for the specified number of days (3, 7, or 10).
    /// </summary>
    public void StartTrial(int days)
    {
        if (days != 3 && days != 7 && days != 10)
            throw new ArgumentException("Trial period must be 3, 7, or 10 days.", nameof(days));

        if (HasUsedTrial)
            throw new InvalidOperationException("Tenant has already used their free trial period.");

        IsTrial = true;
        TrialDays = days;
        TrialEndsAtUtc = DateTime.UtcNow.AddDays(days);
        SubscriptionExpiresAt = TrialEndsAtUtc;
        BillingCycle = SubscriptionBillingCycle.Trial;
        HasUsedTrial = true;
        Status = TenantStatus.Active;
        GracePeriodEndsAt = null;
        Touch();

        RaiseDomainEvent(new TenantTrialStartedEvent(Id, days, TrialEndsAtUtc.Value));
    }

    /// <summary>
    /// Activates or renews a subscription for a given tier and billing cycle (Monthly, Yearly, OneTime).
    /// </summary>
    public void Subscribe(SubscriptionTier tier, SubscriptionBillingCycle cycle, DateTime? customExpiresAt = null)
    {
        if (cycle == SubscriptionBillingCycle.Trial)
            throw new ArgumentException("Use StartTrial() to initiate a trial.", nameof(cycle));

        SubscriptionTier = tier;
        BillingCycle = cycle;
        IsTrial = false;

        if (cycle == SubscriptionBillingCycle.OneTime)
        {
            SubscriptionExpiresAt = null; // Lifetime access
        }
        else if (customExpiresAt.HasValue)
        {
            SubscriptionExpiresAt = customExpiresAt.Value;
        }
        else
        {
            // If already active with future expiration, append from that date; otherwise from now
            var baseDate = (SubscriptionExpiresAt.HasValue && SubscriptionExpiresAt.Value > DateTime.UtcNow)
                ? SubscriptionExpiresAt.Value
                : DateTime.UtcNow;

            SubscriptionExpiresAt = cycle switch
            {
                SubscriptionBillingCycle.Monthly => baseDate.AddMonths(1),
                SubscriptionBillingCycle.Yearly => baseDate.AddYears(1),
                _ => baseDate.AddMonths(1)
            };
        }

        Status = TenantStatus.Active;
        GracePeriodEndsAt = null;
        SetQuotasForTier(tier);
        Touch();

        RaiseDomainEvent(new TenantSubscribedEvent(Id, tier, cycle, SubscriptionExpiresAt));
    }

    /// <summary>
    /// Extends an active trial by a given number of days (e.g. granted by admin or support).
    /// </summary>
    public void ExtendTrial(int additionalDays)
    {
        if (additionalDays <= 0)
            throw new ArgumentException("Additional days must be greater than zero.", nameof(additionalDays));

        if (!IsTrial)
            throw new InvalidOperationException("Tenant is not currently on a trial.");

        TrialDays = (TrialDays ?? 0) + additionalDays;
        TrialEndsAtUtc = (TrialEndsAtUtc.HasValue && TrialEndsAtUtc.Value > DateTime.UtcNow ? TrialEndsAtUtc.Value : DateTime.UtcNow).AddDays(additionalDays);
        SubscriptionExpiresAt = TrialEndsAtUtc;
        Status = TenantStatus.Active;
        GracePeriodEndsAt = null;
        Touch();
    }

    /// <summary>
    /// Evaluates current UTC time against trial/subscription expiry and transitions status if necessary.
    /// </summary>
    public TenantStatus CheckAndRefreshStatus()
    {
        if (Status == TenantStatus.Cancelled)
            return Status;

        // One-time lifetime licenses never expire
        if (BillingCycle == SubscriptionBillingCycle.OneTime)
        {
            if (Status != TenantStatus.Active)
            {
                Status = TenantStatus.Active;
                Touch();
            }
            return Status;
        }

        var now = DateTime.UtcNow;

        if (IsTrial && TrialEndsAtUtc.HasValue && now > TrialEndsAtUtc.Value)
        {
            if (GracePeriodEndsAt.HasValue && now > GracePeriodEndsAt.Value)
            {
                if (Status != TenantStatus.Suspended)
                {
                    Status = TenantStatus.Suspended;
                    Touch();
                }
            }
            else if (Status == TenantStatus.Active)
            {
                // Give 3 days grace period after trial ends
                EnterGracePeriod(now.AddDays(3));
            }
        }
        else if (SubscriptionExpiresAt.HasValue && now > SubscriptionExpiresAt.Value)
        {
            if (GracePeriodEndsAt.HasValue && now > GracePeriodEndsAt.Value)
            {
                if (Status != TenantStatus.Suspended)
                {
                    Status = TenantStatus.Suspended;
                    Touch();
                }
            }
            else if (Status == TenantStatus.Active)
            {
                // Standard 7 days grace period after subscription ends
                EnterGracePeriod(now.AddDays(7));
            }
        }

        return Status;
    }

    public void UpdateBranding(string? logoUrl, string? primaryColor, string? timeZone)
    {
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        TimeZone = timeZone;
        Touch();
    }

    public void UpdateName(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Name = newName.Trim();
        Touch();
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private void SetQuotasForTier(SubscriptionTier tier)
    {
        (MaxUsers, MaxFarms, MaxAnimals) = tier switch
        {
            SubscriptionTier.Starter => (3, 1, 100),
            SubscriptionTier.Standard => (10, 5, 500),
            SubscriptionTier.Professional => (50, 20, 5000),
            SubscriptionTier.Enterprise => (int.MaxValue, int.MaxValue, int.MaxValue),
            _ => (3, 1, 100)
        };
    }
}

// ── Domain Events ─────────────────────────────────────────────────────────────
public sealed record TenantCreatedEvent(Guid TenantId, string Slug) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TenantSuspendedEvent(Guid TenantId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TenantTrialStartedEvent(Guid TenantId, int Days, DateTime EndsAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record TenantSubscribedEvent(
    Guid TenantId,
    SubscriptionTier Tier,
    SubscriptionBillingCycle BillingCycle,
    DateTime? ExpiresAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
