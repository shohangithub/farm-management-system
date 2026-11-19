using Microsoft.AspNetCore.Authorization;

namespace Farm360.Api.Authorization;

/// <summary>Named (non-permission) authorization policies.</summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Cross-tenant platform administration. Requires the <c>sys=true</c> claim, which is only
    /// issued to users flagged <c>IsSystemUser</c>. Tenant roles (including Owner) never satisfy it.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    public const string SystemUserClaim = "sys";

    public static AuthorizationBuilder AddFarm360Policies(this AuthorizationBuilder builder) =>
        builder.AddPolicy(PlatformAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(SystemUserClaim, "true"));
}
