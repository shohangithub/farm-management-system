using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Farm360.Infrastructure.Caching;

/// <summary>
/// Redis-backed distributed cache service.
/// F360-MTA-2026-001 §5 (Layer 5): Cache keys are tenant-scoped.
/// ALL cache keys MUST be prefixed with {tenantId}: to prevent cross-tenant data leakage.
/// Constitution §20 (Performance): Financial data NEVER cached.
/// Use CacheKeyBuilder to construct keys — never raw strings.
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache cache,
    ILogger<RedisCacheService> logger)
    : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await cache.GetStringAsync(key, cancellationToken);

            if (data is null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(data, JsonOptions);
        }
        catch (Exception ex)
        {
            // Cache failures MUST NOT crash the application — degrade gracefully.
            logger.LogWarning(ex, "Cache GET failed for key: {CacheKey}. Proceeding without cache.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = JsonSerializer.Serialize(value, JsonOptions);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration,
            };

            await cache.SetStringAsync(key, data, options, cancellationToken);
        }
        catch (Exception ex)
        {
            // Cache failures are non-fatal — application continues without caching.
            logger.LogWarning(ex, "Cache SET failed for key: {CacheKey}. Data not cached.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache REMOVE failed for key: {CacheKey}.", key);
        }
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // Redis SCAN-based prefix removal requires StackExchange.Redis IServer.
        // Implemented via IServer.Keys pattern in the full implementation.
        // Placeholder for scaffolding — implement when StackExchange.Redis IServer is injected.
        logger.LogWarning("RemoveByPrefixAsync for prefix '{Prefix}' — IServer implementation required.", prefix);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cache key builder — enforces tenant-scoped naming convention.
/// F360-MTA-2026-001: Pattern: {tenantId}:{domain}:{entity}:{discriminator}
/// NEVER construct cache keys as raw strings in application code.
/// </summary>
public static class CacheKeyBuilder
{
    /// <summary>
    /// Builds a tenant-scoped cache key.
    /// Example: "f47ac10b-58cc:livestock:animals:list:active"
    /// </summary>
    public static string Build(Guid tenantId, string domain, string entity, params string[] discriminators)
    {
        var tenantPrefix = tenantId.ToString("N")[..12]; // Short prefix
        var parts = new[] { tenantPrefix, domain, entity }.Concat(discriminators);
        return string.Join(":", parts).ToLowerInvariant();
    }

    /// <summary>Builds a tenant-level prefix for RemoveByPrefix operations.</summary>
    public static string TenantPrefix(Guid tenantId, string domain) =>
        $"{tenantId:N[..12]}:{domain}:".ToLowerInvariant();
}
