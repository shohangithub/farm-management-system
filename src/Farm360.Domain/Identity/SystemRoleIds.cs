using System;

namespace Farm360.Domain.Identity;

/// <summary>
/// System Role IDs — deterministic GUIDs for seeded roles.
/// F360-AUTH-2026-001 §7.2 (System Roles).
/// These GUIDs are stable across all environments.
/// </summary>
public static class SystemRoleIds
{
    public static readonly Guid Owner         = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid FarmManager   = new("10000000-0000-0000-0000-000000000002");
    public static readonly Guid Veterinarian  = new("10000000-0000-0000-0000-000000000003");
    public static readonly Guid Worker        = new("10000000-0000-0000-0000-000000000004");
    public static readonly Guid Viewer        = new("10000000-0000-0000-0000-000000000005");
}
