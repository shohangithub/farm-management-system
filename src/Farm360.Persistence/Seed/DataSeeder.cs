using Farm360.Domain.Identity;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Farm360.Persistence.Seed;

/// <summary>
/// Idempotent seed runner for all static Farm360 reference data.
/// F360-AUTH-2026-001 §7.1: Permissions and System Roles are seeded — never user-created.
/// Constitution §13 (Migration Rules): Seed data is separate from EF migrations.
///
/// Run on app startup in Development and Staging. In Production: run explicitly via CLI.
/// Idempotent: safe to run multiple times. Uses HasData only for deterministic GUIDs.
///
/// Seed order (FK dependency order):
///   1. Permissions
///   2. System Roles
///   3. RolePermissions (Owner→all, FarmManager→most, etc.)
/// </summary>
public sealed class DataSeeder(
    ApplicationDbContext context,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Farm360 DataSeeder: Starting idempotent seed...");

        await SeedPermissionsAsync(cancellationToken);
        await SeedSystemRolesAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);

        logger.LogInformation("Farm360 DataSeeder: Completed.");
    }

    // ── 1. Permissions ────────────────────────────────────────────────────────
    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await context.Permissions
            .AsNoTracking()
            .Select(p => p.Code)
            .ToHashSetAsync(cancellationToken);

        var toAdd = PermissionConstants.All
            .Where(p => !existingCodes.Contains(p.Code))
            .Select((p, index) => Permission.Seed(
                DeterministicGuid("perm", p.Code),
                p.Code, p.Module, p.Description))
            .ToList();

        if (toAdd.Count > 0)
        {
            context.Permissions.AddRange(toAdd);
            await context.SaveChangesAsync(cancellationToken);
            if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                logger.LogInformation("Farm360 DataSeeder: Seeded {Count} permissions", toAdd.Count);
        }
    }

    // ── 2. System Roles ───────────────────────────────────────────────────────
    private async Task SeedSystemRolesAsync(CancellationToken cancellationToken)
    {
        var systemRoles = new[]
        {
            Role.CreateSystemRole(SystemRoleIds.Owner,        "Owner",       "Full access. Account owner."),
            Role.CreateSystemRole(SystemRoleIds.FarmManager,  "FarmManager", "Manages farm operations, animals, feeding, health."),
            Role.CreateSystemRole(SystemRoleIds.Veterinarian, "Veterinarian","Views animals, full health access, can prescribe."),
            Role.CreateSystemRole(SystemRoleIds.Worker,       "Worker",      "Records feeding, basic animal operations."),
            Role.CreateSystemRole(SystemRoleIds.Viewer,       "Viewer",      "Read-only access to all modules."),
        };

        var existingIds = await context.Roles
            .AsNoTracking()
            .Where(r => r.IsSystemRole)
            .Select(r => r.Id)
            .ToHashSetAsync(cancellationToken);

        var toAdd = systemRoles.Where(r => !existingIds.Contains(r.Id)).ToList();

        if (toAdd.Count > 0)
        {
            context.Roles.AddRange(toAdd);
            await context.SaveChangesAsync(cancellationToken);
            if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                logger.LogInformation("Farm360 DataSeeder: Seeded {Count} system roles", toAdd.Count);
        }
    }

    // ── 3. Role → Permission mappings ─────────────────────────────────────────
    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Code, p => p.Id, cancellationToken);

        var existing = await context.RolePermissions
            .AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(cancellationToken);

        var existingSet = existing.Select(e => (e.RoleId, e.PermissionId)).ToHashSet();

        var mappings = BuildRolePermissionMappings(permissions);
        var toAdd = mappings
            .Where(m => !existingSet.Contains((m.roleId, m.permId)))
            .Select(m => new RolePermission(m.roleId, m.permId))
            .ToList();

        if (toAdd.Count > 0)
        {
            context.RolePermissions.AddRange(toAdd);
            await context.SaveChangesAsync(cancellationToken);
            if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                logger.LogInformation("Farm360 DataSeeder: Seeded {Count} role-permission mappings", toAdd.Count);
        }
    }

    // ── Role → Permission Matrix ──────────────────────────────────────────────
    private static List<(Guid roleId, Guid permId)> BuildRolePermissionMappings(Dictionary<string, Guid> perms)
    {
        var result = new List<(Guid, Guid)>();

        // Owner — ALL permissions
        foreach (var (_, permId) in perms)
            result.Add((SystemRoleIds.Owner, permId));

        // FarmManager — everything except billing management
        var farmManagerPerms = perms.Keys
            .Where(c => c != PermissionConstants.Billing.Manage && c != PermissionConstants.Users.AssignRole)
            .ToList();
        foreach (var code in farmManagerPerms)
            result.Add((SystemRoleIds.FarmManager, perms[code]));

        // Veterinarian — animals (view), health (all), feeding (view), reports (view), notifications
        var vetPerms = new[]
        {
            PermissionConstants.Animals.View,
            PermissionConstants.HealthModule.View,
            PermissionConstants.HealthModule.Create,
            PermissionConstants.HealthModule.Edit,
            PermissionConstants.HealthModule.Delete,
            PermissionConstants.HealthModule.Prescribe,
            PermissionConstants.Feeding.View,
            PermissionConstants.Reports.View,
            PermissionConstants.Notifications.View,
        };
        foreach (var code in vetPerms)
            if (perms.TryGetValue(code, out var permId))
                result.Add((SystemRoleIds.Veterinarian, permId));

        // Worker — animals (view, create), feeding (all), inventory (view), notifications
        var workerPerms = new[]
        {
            PermissionConstants.Animals.View, PermissionConstants.Animals.Create,
            PermissionConstants.Feeding.View, PermissionConstants.Feeding.Create, PermissionConstants.Feeding.Edit,
            PermissionConstants.Inventory.View,
            PermissionConstants.Notifications.View,
        };
        foreach (var code in workerPerms)
            if (perms.TryGetValue(code, out var permId))
                result.Add((SystemRoleIds.Worker, permId));

        // Viewer — all *.view permissions only
        var viewerPerms = perms.Keys.Where(c => c.EndsWith(".view", StringComparison.OrdinalIgnoreCase));
        foreach (var code in viewerPerms)
            result.Add((SystemRoleIds.Viewer, perms[code]));

        return result;
    }

    // ── Deterministic GUID generator for permissions ──────────────────────────
    // Uses SHA256 (first 16 bytes) to produce a stable GUID from a string key.
    // CA5351: SHA256 is used for non-security GUID derivation (not cryptographic).
    private static Guid DeterministicGuid(string prefix, string value)
    {
        var input = $"{prefix}:{value}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        // Take first 16 bytes to form a valid GUID
        return new Guid(hash.AsSpan(0, 16));
    }
}
