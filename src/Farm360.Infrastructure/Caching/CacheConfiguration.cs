using Microsoft.Extensions.Configuration;

namespace Farm360.Infrastructure.Caching;

/// <summary>
/// Chooses the distributed-cache backend.
/// <c>Cache:Provider</c> = <c>Auto</c> (default) | <c>Redis</c> | <c>Memory</c>.
/// <list type="bullet">
///   <item><c>Auto</c>: Redis when <c>ConnectionStrings:Redis</c> is set, otherwise in-process memory.</item>
///   <item><c>Redis</c>: required for more than one API instance (tenant/permission cache and
///   Data Protection keys shared by all instances). Fails at startup if the connection string is missing.</item>
///   <item><c>Memory</c>: single instance only (e.g. shared hosting without Redis).</item>
/// </list>
/// </summary>
public static class CacheConfiguration
{
    public const string ProviderKey = "Cache:Provider";

    public static string? RedisConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("Redis");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool UsesRedis(IConfiguration configuration)
    {
        var provider = configuration[ProviderKey];
        if (string.Equals(provider, "Memory", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(provider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            return RedisConnectionString(configuration) is not null
                ? true
                : throw new InvalidOperationException("Cache:Provider is 'Redis' but ConnectionStrings:Redis is not configured.");
        }

        return RedisConnectionString(configuration) is not null;
    }
}
