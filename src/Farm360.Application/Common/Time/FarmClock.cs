using System.Collections.Concurrent;
using Farm360.Application.Common.Interfaces;

namespace Farm360.Application.Common.Time;

/// <summary>Process-wide cache of resolved farm zones (singleton). Zone ids change rarely.</summary>
public sealed class FarmTimeZoneCache
{
    private readonly ConcurrentDictionary<(Guid TenantId, Guid FarmId), (TimeZoneInfo Zone, DateTime ExpiresUtc)> _zones = new();

    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(10);

    public TimeZoneInfo GetOrAdd(Guid tenantId, Guid? farmId, DateTime utcNow, Func<TimeZoneInfo> resolve)
    {
        var key = (tenantId, farmId ?? Guid.Empty);
        if (_zones.TryGetValue(key, out var cached) && cached.ExpiresUtc > utcNow)
        {
            return cached.Zone;
        }

        var zone = resolve();
        _zones[key] = (zone, utcNow + Lifetime);
        return zone;
    }

    /// <summary>Forget a tenant's zones (call after an organization / tenant zone changes).</summary>
    public void Invalidate(Guid tenantId)
    {
        foreach (var key in _zones.Keys)
        {
            if (key.TenantId == tenantId)
            {
                _zones.TryRemove(key, out _);
            }
        }
    }
}

/// <inheritdoc cref="IFarmClock"/>
public sealed class FarmClock : IFarmClock
{
    private readonly ITenantService? _tenant;
    private readonly IFarmTimeZoneSource? _source;
    private readonly IDateTimeService? _dateTime;
    private readonly FarmTimeZoneCache _cache;

    public FarmClock(
        ITenantService? tenant,
        IFarmTimeZoneSource? source,
        IDateTimeService? dateTime = null,
        FarmTimeZoneCache? cache = null)
    {
        _tenant = tenant;
        _source = source;
        _dateTime = dateTime;
        _cache = cache ?? new FarmTimeZoneCache();
    }

    /// <summary>A clock fixed to the default (Bangladesh) zone; for code and tests with no DI.</summary>
    public static IFarmClock System { get; } = new FarmClock(null, null);

    public DateTime UtcNow => _dateTime?.UtcNow ?? DateTime.UtcNow;

    public TimeZoneInfo TimeZone => ZoneFor(_tenant?.TenantId ?? Guid.Empty);

    public DateOnly Today => FarmTimeZones.Today(UtcNow, TimeZone);

    public DateTime Now => FarmTimeZones.ToLocal(UtcNow, TimeZone);

    public DateOnly TodayFor(Guid tenantId) => FarmTimeZones.Today(UtcNow, ZoneFor(tenantId));

    public DateOnly TodayForFarm(Guid tenantId, Guid farmId) => FarmTimeZones.Today(UtcNow, ZoneFor(tenantId, farmId));

    public TimeZoneInfo ZoneFor(Guid tenantId, Guid? farmId = null)
    {
        if (_source is null || (tenantId == Guid.Empty && farmId is null))
        {
            return FarmTimeZones.Default;
        }

        var source = _source;
        return _cache.GetOrAdd(tenantId, farmId, UtcNow, () =>
        {
            var candidates = source.GetZoneCandidates(tenantId, farmId);
            return FarmTimeZones.Resolve(candidates is null ? [] : [.. candidates]);
        });
    }
}
