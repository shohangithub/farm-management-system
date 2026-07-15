using Microsoft.AspNetCore.Authorization;

namespace Farm360.Api.Authorization;

/// <summary>
/// Declarative permission-based authorization attribute.
/// F360-AUTH-2026-001 §7.3 (Attribute-Based Authorization).
///
/// Usage on controllers/actions:
///   [RequirePermission(PermissionConstants.Animals.View)]
///   [RequirePermission(PermissionConstants.Health.Prescribe)]
///
/// Internally registers a named policy: "Permission:{permissionCode}"
/// The policy is registered dynamically via IAuthorizationPolicyProvider.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string permissionCode)
    : AuthorizeAttribute($"Permission:{permissionCode}")
{
    public string PermissionCode { get; } = permissionCode;
}

/// <summary>
/// Dynamic policy provider that creates permission policies on-demand.
/// Registers: "Permission:{code}" → PermissionRequirement(code)
/// Eliminates the need to pre-register every permission as a named policy at startup.
/// </summary>
public sealed class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options = options.Value;
    private const string PolicyPrefix = "Permission:";

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var code = policyName[PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(code))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Fall back to default policies (e.g. [Authorize] with no policy)
        return Task.FromResult(_options.GetPolicy(policyName));
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(_options.DefaultPolicy);

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => Task.FromResult(_options.FallbackPolicy);
}
