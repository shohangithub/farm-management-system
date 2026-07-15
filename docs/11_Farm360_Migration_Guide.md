# Farm360 AI — Database Migration Guide
**Reference:** `F360-CONST-2026-001 §13` (Migration Rules) | `F360-MTA-2026-001` (Multi-Tenant)  
**Author:** Farm360 Engineering  
**Last Updated:** 2026-07-15  
**Status:** ✅ Approved — Source of Truth

---

## 1. Overview

Farm360 uses **two separate EF Core DbContexts** with independent migration histories:

| Context | Project | Schema | Contains |
|---|---|---|---|
| `IdentityDbContext` | `Farm360.Identity` | `identity.*` | Users, Sessions, Devices, OTP, AuthAuditLogs, ExternalProviders |
| `ApplicationDbContext` | `Farm360.Persistence` | `app.*` | Tenant, Organization, Branch, Permission, Role, RolePermission, TenantUser, AuditLog, Notification + all business modules |

**Why two contexts?**  
Identity data (auth tokens, sessions) must be queryable even when tenant context is absent (e.g., during login). Business data is always tenant-scoped. Separating them prevents accidental cross-contamination and keeps migration histories clean.

---

## 2. Migration Rules (Constitution §13)

> **These rules are immutable. Never violate them.**

1. **Never edit a committed migration.** If a mistake is found, write a new migration to correct it.  
2. **Never use `Update-Database` directly in Production.** All migrations run via the CI/CD pipeline or explicit script approval.  
3. **All migrations must be idempotent.** Use `IF NOT EXISTS` guards in raw SQL where needed.  
4. **Seed data is separate from migrations.** Do not put `DataSeeder.SeedAsync()` inside a migration file. Run the seeder after migration.  
5. **Never `DROP COLUMN` or `DROP TABLE` in a forward migration without a data backup plan.**  
6. **Column renames require a 3-phase migration** (add new → copy data → drop old). Never rename directly.  
7. **All FKs must have explicit `OnDelete` behavior.** No implicit cascade deletes.  
8. **Migration names must be descriptive.** Pattern: `{Area}_{Description}` (e.g., `Tenancy_AddOrganizationEmailField`).

---

## 3. Project Setup Prerequisites

Before running any migration, ensure the `design-time` factory is in place.

### `ApplicationDbContextFactory` (Farm360.Persistence)

```csharp
// src/Farm360.Persistence/Context/ApplicationDbContextFactory.cs
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

        // Design-time: pass a no-op TenantService
        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }
}
```

> **CRITICAL:** The `ApplicationDbContext` requires an `ITenantService` at runtime. For design-time (migrations), inject a `DesignTimeTenantService` that returns `Guid.Empty`.

### `DesignTimeTenantService` stub (used only for migrations)

```csharp
internal sealed class DesignTimeTenantService : ITenantService
{
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => "design-time";
    public string TenantName => "Design Time";
    public string SubscriptionTier => "Starter";
    public string TenantStatus => "Active";
    public bool IsActive => true;
    public bool IsGracePeriod => false;
    public void SetTenant(Guid t, string s, string n, string tier, string status) { }
}
```

---

## 4. EF CLI Tool Reference

All commands are run from the **solution root** (`d:\Personel\Farm Management System\`).

```bash
# Install EF Core tools globally (one-time)
dotnet tool install --global dotnet-ef

# Verify installation
dotnet ef --version
```

---

## 5. Running Migrations

### 5.1 Identity Context — `identity.*` schema

```bash
# Add new migration
dotnet ef migrations add InitialIdentity `
    --project "src/Farm360.Identity/Farm360.Identity.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context IdentityDbContext `
    --output-dir "Migrations/Identity"

# Apply to database (Development only)
dotnet ef database update `
    --project "src/Farm360.Identity/Farm360.Identity.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context IdentityDbContext

# Generate SQL script (for Production review)
dotnet ef migrations script `
    --project "src/Farm360.Identity/Farm360.Identity.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context IdentityDbContext `
    --idempotent `
    --output "scripts/identity_migration.sql"
```

### 5.2 Application Context — `app.*` schema

```bash
# Add new migration
dotnet ef migrations add InitialTenancy `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context ApplicationDbContext `
    --output-dir "Migrations/Application"

# Apply to database (Development only)
dotnet ef database update `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context ApplicationDbContext

# Generate SQL script (for Production review)
dotnet ef migrations script `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context ApplicationDbContext `
    --idempotent `
    --output "scripts/application_migration.sql"
```

### 5.3 Remove the last migration (if not yet applied)

```bash
# Remove from Identity context
dotnet ef migrations remove `
    --project "src/Farm360.Identity/Farm360.Identity.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context IdentityDbContext

# Remove from Application context
dotnet ef migrations remove `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context ApplicationDbContext
```

---

## 6. Seed Data After Migration

After applying migrations, run the idempotent `DataSeeder`:

```bash
# Option A: Via App Startup (Development/Staging)
# In Program.cs (already wired):
# app.Services.GetRequiredService<DataSeeder>().SeedAsync().GetAwaiter().GetResult();

# Option B: Via CLI (Production)
dotnet run --project "src/Farm360.Api" -- --seed
```

The `DataSeeder` seeds in order:
1. **Permissions** (42 permission codes for all MVP modules)  
2. **System Roles** (Owner, FarmManager, Veterinarian, Worker, Viewer)  
3. **RolePermissions** (full permission matrix)

> Seeder is **idempotent** — safe to run multiple times. Uses `IF NOT EXISTS` logic via EF queries.

---

## 7. Adding a Business Module Migration

When adding a new module (e.g., `Livestock`):

```bash
# Step 1: Create entity + EF configuration in Farm360.Domain + Farm360.Persistence
# Step 2: Register DbSet in ApplicationDbContext
# Step 3: Add descriptive migration

dotnet ef migrations add Livestock_AddAnimalEntity `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj" `
    --context ApplicationDbContext `
    --output-dir "Migrations/Application"
```

**Migration naming convention:**
```
{Module}_{Description}
Tenancy_AddOrganizationEmailField
Livestock_AddAnimalVaccinationTable
Health_AddTreatmentRecordIndex
```

---

## 8. Multi-Tenant Migration Strategy

### Shared Database (Current Phase)
All tenants share the same database. Tenant isolation is enforced via:
- **EF Global Query Filters**: `WHERE TenantId = @currentTenantId` (evaluated at query time)
- **AuditSaveChangesInterceptor**: Blocks cross-tenant writes at the persistence layer

### Future: Database-Per-Tenant Migration
Defined in `F360-MTA-2026-001 §7.4`. When activated:
1. Each tenant gets their own SQL Server database
2. `ApplicationDbContextFactory` becomes `TenantAwareDbContextFactory`
3. Connection string is resolved per-request from tenant registry
4. Migrations run per-tenant database via admin tool

No code changes needed in Domain or Application layers — only Persistence and Infrastructure.

---

## 9. Global Query Filter Reference

`ApplicationDbContext` applies two global filters automatically:

| Filter | Interface | SQL Equivalent |
|---|---|---|
| Tenant isolation | `ITenantEntity` | `WHERE TenantId = @currentTenantId` |
| Soft delete | `ISoftDeletable` | `WHERE IsDeleted = 0` |

### Bypassing filters (admin/migration code only)

```csharp
// Bypass BOTH filters
context.Organizations.IgnoreQueryFilters().ToListAsync();

// Bypass soft delete only (tenant filter still active)
// Not directly supported — use IgnoreQueryFilters() + manual IsDeleted check:
context.Organizations
    .IgnoreQueryFilters()
    .Where(o => o.TenantId == tenantId) // re-apply tenant filter manually
    .ToListAsync();
```

> **NEVER bypass filters in application code.** Only in admin tools, migration scripts, and DataSeeder.

---

## 10. Schema Reference

### `identity.*` schema

| Table | Context | Purpose |
|---|---|---|
| `identity.Users` | IdentityDbContext | ASP.NET Identity ApplicationUser |
| `identity.Roles` | IdentityDbContext | ASP.NET Identity Roles (IdentityRole) |
| `identity.UserRoles` | IdentityDbContext | ASP.NET Identity User→Role mapping |
| `identity.UserClaims` | IdentityDbContext | Per-user claims |
| `identity.UserLogins` | IdentityDbContext | External login providers |
| `identity.UserTokens` | IdentityDbContext | Token storage |
| `identity.RoleClaims` | IdentityDbContext | Role-level claims |
| `identity.UserSessions` | IdentityDbContext | Refresh tokens (hashed) |
| `identity.UserDevices` | IdentityDbContext | Remember-device tokens |
| `identity.OtpVerifications` | IdentityDbContext | OTP audit records |
| `identity.AuthAuditLogs` | IdentityDbContext | Auth event audit log |
| `identity.ExternalProviders` | IdentityDbContext | OAuth provider links |

### `app.*` schema

| Table | Context | Purpose |
|---|---|---|
| `app.Tenants` | ApplicationDbContext | SaaS tenants (top-level accounts) |
| `app.Organizations` | ApplicationDbContext | Legal entities within tenants |
| `app.Branches` | ApplicationDbContext | Physical locations within organizations |
| `app.Permissions` | ApplicationDbContext | All granular permission codes |
| `app.Roles` | ApplicationDbContext | Farm360 custom roles |
| `app.RolePermissions` | ApplicationDbContext | Role → Permission mappings |
| `app.TenantUsers` | ApplicationDbContext | User → Tenant membership |
| `app.AuditLogs` | ApplicationDbContext | Business audit trail (INSERT-only) |
| `app.Notifications` | ApplicationDbContext | In-app user notifications |

---

## 11. Rollback Strategy

### Development
```bash
# Roll back to specific migration
dotnet ef database update {MigrationName} `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj"

# Roll back ALL migrations
dotnet ef database update 0 `
    --project "src/Farm360.Persistence/Farm360.Persistence.csproj" `
    --startup-project "src/Farm360.Api/Farm360.Api.csproj"
```

### Production
1. Generate the down-script: `dotnet ef migrations script {Target} {Current} --idempotent`
2. Review with DBA
3. Take full backup
4. Apply down-script via DBA-approved process
5. Never run `database update` directly in Production

---

## 12. CI/CD Integration

```yaml
# GitHub Actions — migration step
- name: Apply EF Migrations
  run: |
    dotnet ef database update \
      --project src/Farm360.Persistence \
      --startup-project src/Farm360.Api \
      --context ApplicationDbContext \
      --connection "${{ secrets.DB_CONNECTION_STRING }}"
    
    dotnet ef database update \
      --project src/Farm360.Identity \
      --startup-project src/Farm360.Api \
      --context IdentityDbContext \
      --connection "${{ secrets.DB_CONNECTION_STRING }}"
```

---

## 13. Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `No DbContext was found` | Missing `IDesignTimeDbContextFactory` | Add factory class to the project |
| `Unable to create object of type ITenantService` | DI not available at design time | Use `ApplicationDbContextFactory` with hardcoded tenant stub |
| `Pending model changes` | Entity changed but no migration added | Run `ef migrations add` |
| `FK constraint violation on seed` | Seeder running before migration | Ensure migrations applied first |
| `NU1109 downgrade` | Transitive package version conflict | Pin all MS Extensions to same version in `Directory.Packages.props` |
| `The entity type requires a primary key` | Missing `HasKey()` in configuration | Add `builder.HasKey(e => e.Id)` in EF config |
