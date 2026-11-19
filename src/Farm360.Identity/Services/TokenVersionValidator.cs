using Farm360.Identity.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Farm360.Identity.Services;

/// <summary>
/// Checks the <c>tv</c> (token version) claim of an access token against the user's current
/// <c>TokenVersion</c>. Incrementing the version (logout-all, password change, admin revoke)
/// therefore invalidates every access token issued before it, not just refresh tokens.
/// The version is cached briefly so this is not a database hit per request.
/// </summary>
public interface ITokenVersionValidator
{
    Task<bool> IsCurrentAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default);

    /// <summary>Drops the cached version so a revoke takes effect on this instance immediately.</summary>
    void Invalidate(Guid userId);
}

public sealed class TokenVersionValidator(IdentityDbContext identityContext, IMemoryCache cache) : ITokenVersionValidator
{
    /// <summary>
    /// Upper bound on how long a revoked access token can keep working on another API instance.
    /// </summary>
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    internal static string CacheKey(Guid userId) => $"auth:tv:{userId:N}";

    public async Task<bool> IsCurrentAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default)
    {
        var current = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await identityContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.TokenVersion)
                .FirstOrDefaultAsync(cancellationToken);
        });

        // Unknown (deleted) user, or a token minted before the latest revoke.
        return current.HasValue && current.Value == tokenVersion;
    }

    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));
}
