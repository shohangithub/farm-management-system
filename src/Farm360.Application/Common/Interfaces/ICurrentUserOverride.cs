namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Lets background work act on behalf of a tenant user in its own DI scope (no HTTP request, so no
/// JWT claims): once set, <see cref="ICurrentUserService"/> in that scope reports this user, tenant
/// and role instead of the request's claims. Used by the demo seeder, which runs the real commands
/// as the demo tenant's owner. Scoped; never set during an HTTP request.
/// </summary>
public interface ICurrentUserOverride
{
    /// <summary>True once <see cref="Set"/> was called in this scope.</summary>
    bool IsSet { get; }

    void Set(Guid userId, Guid tenantId, string role, string? subscriptionTier = null);
}
