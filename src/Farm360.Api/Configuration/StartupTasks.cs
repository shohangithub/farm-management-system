using Microsoft.EntityFrameworkCore;

namespace Farm360.Api.Configuration;

/// <summary>
/// One-shot work done once per process start, before the app accepts traffic.
/// <list type="number">
///   <item><b>Migrations</b> — only when <c>Database:MigrateOnStartup</c> is true (default: Development only).
///   A failure is fatal: the process logs it and exits instead of serving requests against a stale schema.
///   In production, apply migrations as a separate deploy step with the <c>efbundle</c> built by CI.</item>
///   <item><b>Reference data</b> — permissions, system roles, role grants and default breeds, every start
///   (idempotent; new permission codes must be granted before anyone calls the new endpoints).</item>
///   <item><b>Demo tenant</b> — only when <c>Seed:DemoData</c> is true (default: Development only).</item>
///   <item><b>System admin</b> — IdentitySeeder; only when <c>Seed:SystemAdmin:*</c> is configured (or Development).</item>
///   <item><b>Feed-allocation backfill</b> (2020–2035, all tenants) — only when
///   <c>Maintenance:RunFeedAllocationBackfillOnStartup</c> is true (default false). For routine repairs use
///   <c>POST /api/v1/feeding/allocations/backfill/{farmId}</c> instead.</item>
/// </list>
/// </summary>
public static class StartupTasks
{
    public static bool MigrateOnStartup(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue("Database:MigrateOnStartup", defaultValue: environment.IsDevelopment());

    public static bool SeedDemoData(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue("Seed:DemoData", defaultValue: environment.IsDevelopment());

    public static bool RunFeedAllocationBackfill(IConfiguration configuration) =>
        configuration.GetValue("Maintenance:RunFeedAllocationBackfillOnStartup", defaultValue: false);

    public static async Task RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // ── 0. Messaging sanity: in production, "Log" means OTPs and invitations never reach anyone ──
        if (!environment.IsDevelopment())
        {
            if (string.Equals(configuration["Sms:Provider"] ?? "Log", "Log", StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("Sms:Provider is 'Log': OTP, password-reset and invitation SMS are NOT sent. Set Sms:Provider=BulkSmsBd.");
            if (string.Equals(configuration["Email:Provider"] ?? "Log", "Log", StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("Email:Provider is 'Log': invitation emails are NOT sent. Set Email:Provider=Smtp.");
        }

        // ── 1. Migrations (fail fast) ─────────────────────────────────────────
        if (MigrateOnStartup(configuration, environment))
        {
            var dbContext = services.GetRequiredService<Farm360.Persistence.Context.ApplicationDbContext>();
            try
            {
                var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                logger.LogInformation("Database:MigrateOnStartup is enabled — applying {Count} pending migration(s).", pending.Count);
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Database migration failed at startup. The API will NOT start against a partially migrated or " +
                    "unreachable database. Fix the cause, or set Database:MigrateOnStartup=false and apply migrations " +
                    "with the efbundle (see docs/deployment.md).");
                throw new InvalidOperationException("Database migration failed at startup.", ex);
            }
        }
        else
        {
            logger.LogInformation(
                "Database:MigrateOnStartup is disabled — migrations must be applied out-of-band (efbundle). " +
                "/health/ready reports pending migrations.");
        }

        // ── 2–4. Seeders ──────────────────────────────────────────────────────
        var includeDemoData = SeedDemoData(configuration, environment);
        var dataSeeder = services.GetRequiredService<Farm360.Persistence.Seed.DataSeeder>();
        await dataSeeder.SeedAsync(includeDemoData, cancellationToken);

        var identitySeeder = services.GetRequiredService<Farm360.Identity.Seed.IdentitySeeder>();
        await identitySeeder.SeedAsync();

        // ── 5. Optional one-off cross-tenant feed-allocation backfill ─────────
        if (RunFeedAllocationBackfill(configuration))
        {
            logger.LogWarning(
                "Maintenance:RunFeedAllocationBackfillOnStartup is enabled — running the 2020–2035 cross-tenant " +
                "feed-allocation backfill. Turn the flag off again once it has completed.");
            try
            {
                var sender = services.GetRequiredService<MediatR.ISender>();
                await sender.Send(
                    new Farm360.Application.Feeding.Jobs.BackfillAnimalFeedAllocationsCommand(
                        From: new DateOnly(2020, 1, 1),
                        To: new DateOnly(2035, 12, 31)),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Idempotent and resumable: a failure here must not keep the API down.
                logger.LogWarning(ex, "Startup feed-allocation backfill did not complete; it can be re-run safely.");
            }
        }
    }
}
