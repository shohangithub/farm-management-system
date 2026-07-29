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
        await SeedBreedsAsync(cancellationToken);

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
            PermissionConstants.FeedingModule.View,
            PermissionConstants.ReportsModule.View,
            PermissionConstants.Notifications.View,
        };
        foreach (var code in vetPerms)
            if (perms.TryGetValue(code, out var permId))
                result.Add((SystemRoleIds.Veterinarian, permId));

        // Worker — animals (view, create), feeding (all), inventory (view), notifications
        var workerPerms = new[]
        {
            PermissionConstants.Animals.View, PermissionConstants.Animals.Create,
            PermissionConstants.FeedingModule.View, PermissionConstants.FeedingModule.Create, PermissionConstants.FeedingModule.Edit,
            PermissionConstants.InventoryModule.View,
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

    // ── 4. Breeds (Tenant-Scoped Master Data) ─────────────────────────────────
    private async Task SeedBreedsAsync(CancellationToken cancellationToken)
    {
        var tenants = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        int addedCount = 0;

        foreach (var tenantId in tenants)
        {
            var existingBreeds = await context.Breeds
                .IgnoreQueryFilters()
                .Where(b => b.TenantId == tenantId)
                .Select(b => b.Name)
                .ToHashSetAsync(cancellationToken);

            var breedsToAdd = new List<Farm360.Domain.Livestock.Breed>();

            // Helper to add breed if it doesn't exist
            void AddBreed(string name, string desc, string cat, string orig, string purp, 
                          decimal adgPoor, decimal adgAvg, decimal adgGood, decimal adgInt, 
                          decimal fcrMin, decimal fcrMax, decimal stdAdgMin, decimal stdAdgMax, 
                          decimal milkMin, decimal milkMax, decimal fatMin, decimal fatMax, string bestFor)
            {
                if (!existingBreeds.Contains(name))
                {
                    breedsToAdd.Add(new Farm360.Domain.Livestock.Breed(
                        Guid.NewGuid(), tenantId, name, desc, cat, orig, purp,
                        adgPoor, adgAvg, adgGood, adgInt, fcrMin, fcrMax, stdAdgMin, stdAdgMax,
                        milkMin, milkMax, fatMin, fatMax, bestFor));
                }
            }

            // ── Indigenous (Native) ──────────────────────────────────────────
            AddBreed("Deshi (Local)", "Hardy, disease-resistant native breed.", "Indigenous", "Bangladesh", "Dual-purpose",
                0.2m, 0.3m, 0.4m, 0.5m, 8.0m, 10.0m, 0.2m, 0.4m, 1.0m, 3.0m, 4.5m, 5.5m, "Low-cost farming");

            AddBreed("Red Chittagong (RCC)", "Native breed known for quality milk and meat.", "Indigenous", "Bangladesh", "Dairy",
                0.2m, 0.3m, 0.4m, 0.5m, 7.0m, 9.0m, 0.3m, 0.5m, 2.0m, 5.0m, 4.5m, 5.0m, "Small dairy farms");

            AddBreed("Pabna", "Native milking breed.", "Indigenous", "Bangladesh", "Dairy",
                0.2m, 0.3m, 0.4m, 0.5m, 8.0m, 10.0m, 0.3m, 0.5m, 5.0m, 10.0m, 4.0m, 4.5m, "Native dairy");

            // ── Exotic (Imported) ────────────────────────────────────────────
            AddBreed("Holstein Friesian", "High-yielding dairy breed.", "Exotic", "Netherlands", "Dairy",
                0.4m, 0.6m, 0.8m, 1.0m, 6.0m, 8.0m, 0.6m, 1.0m, 20.0m, 35.0m, 3.4m, 3.8m, "High-volume commercial dairy");

            AddBreed("Jersey", "High butterfat dairy breed.", "Exotic", "UK", "Dairy",
                0.4m, 0.5m, 0.6m, 0.8m, 6.0m, 8.0m, 0.5m, 0.8m, 15.0m, 25.0m, 4.8m, 5.5m, "Premium milk (high butterfat)");

            AddBreed("Sahiwal", "Heat-tolerant dairy breed.", "Exotic", "Pakistan", "Dairy",
                0.4m, 0.5m, 0.7m, 0.8m, 6.5m, 8.5m, 0.5m, 0.8m, 8.0m, 15.0m, 4.5m, 5.0m, "Heat-tolerant dairy");

            AddBreed("Red Sindhi", "Heat-tolerant dairy breed.", "Exotic", "Pakistan", "Dairy",
                0.3m, 0.4m, 0.6m, 0.7m, 6.5m, 8.5m, 0.4m, 0.7m, 8.0m, 12.0m, 4.5m, 5.0m, "Dairy");

            AddBreed("Hariana", "Dual-purpose exotic breed.", "Exotic", "India", "Dual-purpose",
                0.3m, 0.4m, 0.6m, 0.7m, 7.0m, 9.0m, 0.4m, 0.7m, 6.0m, 10.0m, 4.0m, 4.5m, "Dual-purpose");

            AddBreed("Brahman", "Heat-tolerant beef breed.", "Exotic", "USA/India", "Beef",
                0.5m, 0.8m, 1.0m, 1.2m, 5.0m, 7.0m, 0.8m, 1.2m, 2.0m, 5.0m, 4.0m, 4.5m, "Beef");

            // ── Crossbred ────────────────────────────────────────────────────
            AddBreed("Holstein Cross", "High-yielding commercial dairy cross.", "Crossbred", "Local Cross", "Dairy",
                0.5m, 0.7m, 0.9m, 1.0m, 6.0m, 8.0m, 0.7m, 1.0m, 12.0m, 25.0m, 3.8m, 4.2m, "Commercial dairy");

            AddBreed("Jersey Cross", "High quality milk cross.", "Crossbred", "Local Cross", "Dairy",
                0.4m, 0.5m, 0.7m, 0.8m, 6.0m, 8.0m, 0.5m, 0.8m, 8.0m, 18.0m, 4.5m, 5.2m, "Quality milk");

            AddBreed("Sahiwal Cross", "Balanced dairy crossbred.", "Crossbred", "Local Cross", "Dairy",
                0.4m, 0.6m, 0.8m, 0.9m, 6.5m, 8.5m, 0.6m, 0.9m, 8.0m, 15.0m, 4.2m, 4.8m, "Balanced dairy");

            AddBreed("Brahman Cross", "High-yielding beef crossbred.", "Crossbred", "Local Cross", "Beef",
                0.6m, 0.9m, 1.1m, 1.3m, 5.0m, 7.0m, 0.9m, 1.3m, 2.0m, 6.0m, 4.0m, 4.5m, "Beef with limited milk");


            if (breedsToAdd.Count > 0)
            {
                context.Breeds.AddRange(breedsToAdd);
                addedCount += breedsToAdd.Count;
            }
        }

        if (addedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                logger.LogInformation("Farm360 DataSeeder: Seeded {Count} default breeds across {TenantCount} tenants", addedCount, tenants.Count);
        }
    }
}
