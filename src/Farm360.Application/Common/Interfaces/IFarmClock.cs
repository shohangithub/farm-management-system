namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// The farm's calendar. "Today" for a farm is the local date in the farm's time zone, not the UTC
/// date: a Bangladesh farm (UTC+6) is already on the next day between 00:00 and 06:00 local while
/// UTC still says yesterday. Every business date ("today", "date cannot be in the future", the
/// day a daily job generates) must come from here. Real instants (CreatedAtUtc, token expiry,
/// audit stamps) stay on UTC.
/// </summary>
/// <remarks>
/// Zone resolution: the farm's organization zone → the tenant's organization zone → the tenant
/// zone → Asia/Dhaka → a fixed UTC+6 zone. Lookups are cached.
/// </remarks>
public interface IFarmClock
{
    /// <summary>The current UTC instant.</summary>
    DateTime UtcNow { get; }

    /// <summary>The time zone of the current request's tenant.</summary>
    TimeZoneInfo TimeZone { get; }

    /// <summary>Today's date in the current request's tenant zone.</summary>
    DateOnly Today { get; }

    /// <summary>The current local wall-clock time in the current request's tenant zone.</summary>
    DateTime Now { get; }

    /// <summary>Today's date for a tenant (for background jobs, which have no request tenant).</summary>
    DateOnly TodayFor(Guid tenantId);

    /// <summary>Today's date for a specific farm (uses the farm's organization zone when it has one).</summary>
    DateOnly TodayForFarm(Guid tenantId, Guid farmId);

    /// <summary>The zone for a tenant, optionally narrowed to one of its farms.</summary>
    TimeZoneInfo ZoneFor(Guid tenantId, Guid? farmId = null);
}

/// <summary>
/// Reads the stored zone ids for a tenant / farm. Implemented in Persistence.
/// Returns candidate ids, most specific first (farm's organization, tenant's organization, tenant).
/// Implementations must be safe to call from any scope (including background jobs).
/// </summary>
public interface IFarmTimeZoneSource
{
    IReadOnlyList<string?> GetZoneCandidates(Guid tenantId, Guid? farmId);
}
