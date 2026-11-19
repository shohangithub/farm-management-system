using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.BusinessRules;

/// <summary>A tenant's stored overrides: the tenant-wide row and any per-farm rows.</summary>
public sealed record TenantRulesSnapshot(
    BusinessRulesOverrides? Tenant,
    IReadOnlyDictionary<Guid, BusinessRulesOverrides> Farms)
{
    public static readonly TenantRulesSnapshot Empty = new(null, new Dictionary<Guid, BusinessRulesOverrides>());
}

/// <summary>
/// Loads a tenant's override rows from the database and caches them per tenant.
/// </summary>
/// <remarks>
/// Singleton: it holds no scoped service. Each load opens its own DI scope, so it never shares (or
/// interferes with) the calling request's DbContext, and it reads with an explicit tenant id, so it
/// works for background jobs iterating tenants. Only the overrides are cached — the configuration
/// baseline is read on every resolution, so an appsettings change still applies without a restart.
/// The cache is per process: on a multi-instance deployment another instance sees a saved change
/// within <see cref="CacheDuration"/>.
/// </remarks>
public sealed class FarmBusinessRulesCache
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<FarmBusinessRules> _options;

    public FarmBusinessRulesCache(IMemoryCache cache, IServiceScopeFactory scopeFactory, IOptionsMonitor<FarmBusinessRules> options)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    /// <summary>The configured platform defaults.</summary>
    public FarmBusinessRules Defaults => _options.CurrentValue ?? new FarmBusinessRules();

    public static string CacheKey(Guid tenantId) => $"farm360:business-rules:{tenantId:N}";

    public TenantRulesSnapshot GetSnapshot(Guid tenantId)
    {
        if (tenantId == Guid.Empty) return TenantRulesSnapshot.Empty;

        return _cache.GetOrCreate(CacheKey(tenantId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.Size = 1;
            return Load(tenantId);
        }) ?? TenantRulesSnapshot.Empty;
    }

    public FarmBusinessRules Resolve(Guid tenantId, Guid? farmId)
    {
        var snapshot = GetSnapshot(tenantId);
        var rules = FarmBusinessRulesResolution.Apply(Defaults, snapshot.Tenant);

        if (farmId is { } f && snapshot.Farms.TryGetValue(f, out var farmOverrides))
            rules = FarmBusinessRulesResolution.Apply(rules, farmOverrides);

        return rules;
    }

    public void Invalidate(Guid tenantId) => _cache.Remove(CacheKey(tenantId));

    private TenantRulesSnapshot Load(Guid tenantId)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetService<ITenantBusinessSettingsRepository>();
        if (repository is null) return TenantRulesSnapshot.Empty; // host without persistence (tests, tools)

        var rows = repository.GetAllForTenant(tenantId);
        var tenantRow = rows.FirstOrDefault(r => r.FarmId is null);
        var farms = rows
            .Where(r => r.FarmId is not null)
            .GroupBy(r => r.FarmId!.Value)
            .ToDictionary(g => g.Key, g => g.First().ToOverrides());

        return new TenantRulesSnapshot(tenantRow?.ToOverrides(), farms);
    }
}

/// <summary>Ambient tenant for code that runs outside a request (see <see cref="IFarmBusinessRulesProvider.BeginTenantScope"/>).</summary>
public static class FarmBusinessRulesScope
{
    private static readonly AsyncLocal<ScopeFrame?> Ambient = new();

    public static (Guid TenantId, Guid? FarmId)? Current =>
        Ambient.Value is { } frame ? (frame.TenantId, frame.FarmId) : null;

    public static IDisposable Begin(Guid tenantId, Guid? farmId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A business-rules scope needs a tenant.", nameof(tenantId));

        var frame = new ScopeFrame(tenantId, farmId, Ambient.Value);
        Ambient.Value = frame;
        return frame;
    }

    private sealed class ScopeFrame(Guid tenantId, Guid? farmId, ScopeFrame? parent) : IDisposable
    {
        private bool _disposed;
        public Guid TenantId { get; } = tenantId;
        public Guid? FarmId { get; } = farmId;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Ambient.Value = parent;
        }
    }
}

/// <summary>
/// Per-request provider: resolves the business rules for the ambient tenant.
/// </summary>
/// <remarks>
/// Scoped because it reads the request tenant from the scoped <see cref="ITenantService"/>. The
/// expensive part (loading override rows) lives in the singleton <see cref="FarmBusinessRulesCache"/>.
/// Resolution order for <see cref="Current"/>: <see cref="FarmBusinessRulesScope"/> → request tenant
/// → configuration defaults. Historical allocations are unaffected by a change: each row stores the
/// method that produced it.
/// </remarks>
public sealed class FarmBusinessRulesProvider : IFarmBusinessRulesProvider
{
    private readonly FarmBusinessRulesCache _cache;
    private readonly ITenantService? _tenantService;

    public FarmBusinessRulesProvider(FarmBusinessRulesCache cache, ITenantService? tenantService = null)
    {
        _cache = cache;
        _tenantService = tenantService;
    }

    public FarmBusinessRules Current
    {
        get
        {
            if (FarmBusinessRulesScope.Current is { } scope)
                return _cache.Resolve(scope.TenantId, scope.FarmId);

            var tenantId = _tenantService?.TenantId ?? Guid.Empty;
            return tenantId == Guid.Empty ? _cache.Defaults.Clone() : _cache.Resolve(tenantId, null);
        }
    }

    public FarmBusinessRules Defaults => _cache.Defaults.Clone();

    public FarmBusinessRules For(Guid tenantId, Guid? farmId = null) =>
        tenantId == Guid.Empty ? _cache.Defaults.Clone() : _cache.Resolve(tenantId, farmId);

    public IDisposable BeginTenantScope(Guid tenantId, Guid? farmId = null) => FarmBusinessRulesScope.Begin(tenantId, farmId);

    public void Invalidate(Guid tenantId) => _cache.Invalidate(tenantId);
}

/// <summary>
/// Falls back to the coded defaults when no configuration section is present.
/// </summary>
/// <remarks>
/// Used by tests and by any host that has not declared a section. The defaults are the decisions
/// recorded in docs/32 §9, so an unconfigured environment behaves the same as a configured one
/// that simply agreed with them.
/// </remarks>
public sealed class DefaultFarmBusinessRulesProvider : IFarmBusinessRulesProvider
{
    private readonly FarmBusinessRules _rules;

    public DefaultFarmBusinessRulesProvider(FarmBusinessRules? rules = null)
    {
        _rules = rules ?? new FarmBusinessRules();
    }

    public FarmBusinessRules Current => _rules;
}
