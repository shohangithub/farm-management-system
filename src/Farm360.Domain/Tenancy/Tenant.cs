using Farm360.Domain.Common;

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
    public TenantStatus Status { get; private set; }
    public DateTime? SubscriptionExpiresAt { get; private set; }
    public DateTime? GracePeriodEndsAt { get; private set; }

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
