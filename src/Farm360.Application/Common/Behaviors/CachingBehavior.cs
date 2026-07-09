using Farm360.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// Marks a query as eligible for caching.
/// Implement on IRequest types (queries only — never commands).
/// Constitution §20 (Performance): Cache TTL defined per query type.
/// F360-MTA-2026-001: Cache keys are tenant-scoped — no cross-tenant data leakage.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// Redis cache key. MUST include TenantId as prefix.
    /// Pattern: {tenantId}:{domain}:{entity}:{discriminator}
    /// </summary>
    string CacheKey { get; }

    /// <summary>Cache TTL. Max: 5 minutes for list data. Financial data: never cache.</summary>
    TimeSpan CacheDuration { get; }
}

/// <summary>
/// MediatR pipeline behavior: transparent cache-aside for read queries.
/// Only activates for queries implementing <see cref="ICacheableQuery"/>.
/// Runs FIFTH in the pipeline (read path only).
/// F360-MTA-2026-001 §5 — Cache key namespacing enforces tenant isolation.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheableQuery)
        {
            return await next();
        }

        var cachedResponse = await cache.GetAsync<TResponse>(cacheableQuery.CacheKey, cancellationToken);

        if (cachedResponse is not null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Farm360 Cache HIT: {CacheKey}", cacheableQuery.CacheKey);
            }

            return cachedResponse;
        }

        // MediatR 12: delegate takes no CancellationToken
        var response = await next();

        await cache.SetAsync(
            cacheableQuery.CacheKey,
            response,
            cacheableQuery.CacheDuration,
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Farm360 Cache MISS \u2192 stored: {CacheKey} (TTL: {TtlSeconds}s)",
                cacheableQuery.CacheKey,
                (int)cacheableQuery.CacheDuration.TotalSeconds);
        }

        return response;
    }
}
