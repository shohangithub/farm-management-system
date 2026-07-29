namespace Farm360.Persistence.Seed;

/// <summary>
/// All permission codes for the Farm360 MVP.
/// Format: {module}.{action}
/// These are seeded by DataSeeder — NEVER deleted.
/// F360-AUTH-2026-001 §7.1 (Permission Registry).
/// </summary>
public static class PermissionConstants
{
    // ── Animals / Livestock ───────────────────────────────────────────────────
    public static class Animals
    {
        public const string View   = "animals.view";
        public const string Create = "animals.create";
        public const string Edit   = "animals.edit";
        public const string Delete = "animals.delete";
        public const string Export = "animals.export";
        public const string Sell   = "animals.sell";
        public const string Quarantine = "animals.quarantine";
    }

    // ── Health / Veterinary ───────────────────────────────────────────────────
    public static class HealthModule
    {
        public const string View   = "health.view";
        public const string Create = "health.create";
        public const string Edit   = "health.edit";
        public const string Delete = "health.delete";
        public const string Prescribe = "health.prescribe";
    }

    // ── Feeding ───────────────────────────────────────────────────────────────
    public static class FeedingModule
    {
        public const string View   = "feeding.view";
        public const string Create = "feeding.create";
        public const string Edit   = "feeding.edit";
        public const string Delete = "feeding.delete";
    }

    // ── Inventory ─────────────────────────────────────────────────────────────
    public static class InventoryModule
    {
        public const string View   = "inventory.view";
        public const string Create = "inventory.create";
        public const string Edit   = "inventory.edit";
        public const string Delete = "inventory.delete";
        public const string Manage = "inventory.manage";
    }

    // ── Reports ───────────────────────────────────────────────────────────────
    public static class ReportsModule
    {
        public const string View   = "reports.view";
        public const string Export = "reports.export";
        public const string Schedule = "reports.schedule";
    }

    // ── Users / Team Management ───────────────────────────────────────────────
    public static class Users
    {
        public const string View   = "users.view";
        public const string Invite = "users.invite";
        public const string Edit   = "users.edit";
        public const string Remove = "users.remove";
        public const string AssignRole = "users.assignrole";
    }

    // ── Roles & Permissions ───────────────────────────────────────────────────
    public static class Roles
    {
        public const string View   = "roles.view";
        public const string Create = "roles.create";
        public const string Edit   = "roles.edit";
        public const string Delete = "roles.delete";
    }

    // ── Organizations & Branches ─────────────────────────────────────────────
    public static class OrganizationModule
    {
        public const string View   = "organizations.view";
        public const string Create = "organizations.create";
        public const string Edit   = "organizations.edit";
        public const string Delete = "organizations.delete";
    }

    // ── Farms ────────────────────────────────────────────────────────────────
    public static class FarmModule
    {
        public const string View   = "farms.view";
        public const string Create = "farms.create";
        public const string Edit   = "farms.edit";
        public const string Delete = "farms.delete";
    }

    public static class ShedModule
    {
        public const string View   = "sheds.view";
        public const string Create = "sheds.create";
        public const string Edit   = "sheds.edit";
        public const string Delete = "sheds.delete";
    }

    public static class PenModule
    {
        public const string View   = "pens.view";
        public const string Create = "pens.create";
        public const string Edit   = "pens.edit";
        public const string Delete = "pens.delete";
    }

    public static class MasterDataModule
    {
        public const string View   = "masterdata.view";
        public const string Manage = "masterdata.manage";
    }

    // ── Settings ─────────────────────────────────────────────────────────────
    public static class Settings
    {
        public const string View   = "settings.view";
        public const string Edit   = "settings.edit";
    }

    // ── Billing & Subscription ────────────────────────────────────────────────
    public static class Billing
    {
        public const string View   = "billing.view";
        public const string Manage = "billing.manage";
    }

    // ── Notifications ─────────────────────────────────────────────────────────
    public static class Notifications
    {
        public const string View   = "notifications.view";
        public const string Manage = "notifications.manage";
    }

    /// <summary>All permissions in the system — used for seeding.</summary>
    public static IReadOnlyList<(string Code, string Module, string Description)> All =>
    [
        (Animals.View,   "Animals", "View animals and livestock records"),
        (Animals.Create, "Animals", "Add new animals to the system"),
        (Animals.Edit,   "Animals", "Edit existing animal records"),
        (Animals.Delete, "Animals", "Delete or archive animal records"),
        (Animals.Export, "Animals", "Export animal data to Excel/PDF"),
        (Animals.Sell,       "Animals", "Record animal sales"),
        (Animals.Quarantine, "Animals", "Place animals under quarantine or release them"),

        (HealthModule.View,      "Health", "View health records and treatments"),
        (HealthModule.Create,    "Health", "Record new health events and treatments"),
        (HealthModule.Edit,      "Health", "Edit health records"),
        (HealthModule.Delete,    "Health", "Delete health records"),
        (HealthModule.Prescribe, "Health", "Issue prescriptions and medication orders"),

        (FeedingModule.View,   "Feeding", "View feeding schedules and records"),
        (FeedingModule.Create, "Feeding", "Record feeding events"),
        (FeedingModule.Edit,   "Feeding", "Edit feeding records"),
        (FeedingModule.Delete, "Feeding", "Delete feeding records"),

        (InventoryModule.View,   "Inventory", "View inventory and stock levels"),
        (InventoryModule.Create, "Inventory", "Add inventory items and stock"),
        (InventoryModule.Edit,   "Inventory", "Update inventory records"),
        (InventoryModule.Delete, "Inventory", "Remove inventory items"),
        (InventoryModule.Manage, "Inventory", "Manage suppliers and stock adjustments"),

        (ReportsModule.View,     "Reports", "View reports and analytics"),
        (ReportsModule.Export,   "Reports", "Export reports to Excel/PDF"),
        (ReportsModule.Schedule, "Reports", "Schedule automated report delivery"),

        (Users.View,       "Users", "View team members"),
        (Users.Invite,     "Users", "Invite new users to the tenant"),
        (Users.Edit,       "Users", "Edit user details"),
        (Users.Remove,     "Users", "Remove users from tenant"),
        (Users.AssignRole, "Users", "Assign roles to users"),

        (Roles.View,   "Roles", "View roles and permissions"),
        (Roles.Create, "Roles", "Create custom roles"),
        (Roles.Edit,   "Roles", "Edit role permissions"),
        (Roles.Delete, "Roles", "Delete custom roles"),

        (OrganizationModule.View,   "Organizations", "View organizations and branches"),
        (OrganizationModule.Create, "Organizations", "Create organizations and branches"),
        (OrganizationModule.Edit,   "Organizations", "Edit organization details"),
        (OrganizationModule.Delete, "Organizations", "Delete organizations"),

        (FarmModule.View,   "Farms", "View farms and locations"),
        (FarmModule.Create, "Farms", "Create new farms"),
        (FarmModule.Edit,   "Farms", "Edit farm details"),
        (FarmModule.Delete, "Farms", "Delete farms"),

        (ShedModule.View,   "Sheds", "View sheds and housing units"),
        (ShedModule.Create, "Sheds", "Create new sheds"),
        (ShedModule.Edit,   "Sheds", "Edit shed details"),
        (ShedModule.Delete, "Sheds", "Delete sheds"),

        (PenModule.View,   "Pens", "View pens within sheds"),
        (PenModule.Create, "Pens", "Create new pens"),
        (PenModule.Edit,   "Pens", "Edit pen details"),
        (PenModule.Delete, "Pens", "Delete pens"),

        (MasterDataModule.View,   "MasterData", "View master data and reference lists"),
        (MasterDataModule.Manage, "MasterData", "Create and edit master data entries"),

        (Settings.View, "Settings", "View tenant settings"),
        (Settings.Edit, "Settings", "Edit tenant settings and branding"),

        (Billing.View,   "Billing", "View billing and subscription information"),
        (Billing.Manage, "Billing", "Manage billing and upgrade subscription"),

        (Notifications.View,   "Notifications", "View notifications"),
        (Notifications.Manage, "Notifications", "Manage notification settings"),
    ];
}


