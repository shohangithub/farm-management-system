using Microsoft.AspNetCore.Authorization;

namespace Farm360.Api.Authorization;

/// <summary>
/// Permission-based authorization requirement.
/// F360-AUTH-2026-001 §7 (Permission-Based Authorization).
/// Used by PermissionHandler to enforce fine-grained access control.
/// </summary>
public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
