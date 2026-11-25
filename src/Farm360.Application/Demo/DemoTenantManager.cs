using System.Diagnostics;
using System.Globalization;
using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farm360.Application.Demo;

/// <summary>
/// Lifecycle of the public demo farm: status, the one-click demo login, the background seed after
/// start-up, the weekly rebuild and the admin rebuild. Hangfire calls the public methods.
/// </summary>
/// <remarks>
/// A rebuild creates a NEW demo tenant, fills it, then moves the Viewer to it and retires (cancels)
/// the older demo tenants, so the demo login always lands on the newest one and a half-built tenant
/// is never shown. Only after that succeeds are the retired demo tenants purged for good
/// (<see cref="DemoTenantPurger"/>).
/// </remarks>
public sealed class DemoTenantManager(
    IOptions<DemoOptions> options,
    IDemoTenantStore store,
    IDemoAccountService accounts,
    IAuthService authService,
    ITenantMembershipService memberships,
    DemoSeeder seeder,
    DemoTenantPurger purger,
    IFarmClock clock,
    ILogger<DemoTenantManager> logger)
{
    /// <summary>One build at a time per process (start-up seed, weekly job and admin rebuild can overlap).</summary>
    private static readonly SemaphoreSlim BuildGate = new(1, 1);

    public bool Enabled => options.Value.Enabled;

    public async Task<DemoStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return new DemoStatusDto(false, false);

        return new DemoStatusDto(true, await store.GetPublishedTenantIdAsync(cancellationToken) is not null);
    }

    /// <summary>
    /// Issues a normal session (JWT + refresh token) for the demo Viewer in the newest demo tenant,
    /// or null when the demo is off or not ready. Takes no input: it can only ever sign in the
    /// fixed demo Viewer account, and only while that account is a plain Viewer of a demo tenant.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return null;

        if (await store.GetPublishedTenantIdAsync(cancellationToken) is not { } tenantId)
            return null;

        var membership = await memberships.GetActiveMembershipAsync(DemoConstants.ViewerUserId, tenantId, cancellationToken);
        if (membership is null
            || membership.TenantId != tenantId
            || !string.Equals(membership.RoleName, "Viewer", StringComparison.Ordinal)
            || !await accounts.IsSafeViewerAsync(cancellationToken))
        {
            logger.LogWarning("Demo login refused: the demo Viewer account or its membership is not in the expected read-only state.");
            return null;
        }

        return await authService.IssueSessionAsync(DemoConstants.ViewerUserId, tenantId, cancellationToken);
    }

    /// <summary>Background job after start-up: builds the demo when it is enabled and none is ready yet.</summary>
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled || await store.GetPublishedTenantIdAsync(cancellationToken) is not null)
            return;

        await BuildAsync(cancellationToken);
    }

    /// <summary>Weekly job: rebuilds the demo (fresh dates) when it is enabled.</summary>
    public async Task RebuildIfEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return;

        await BuildAsync(cancellationToken);
    }

    /// <summary>Admin rebuild: builds a fresh demo tenant now, even while the public demo is switched off.</summary>
    public Task<bool> RebuildAsync(CancellationToken cancellationToken = default) => BuildAsync(cancellationToken);

    /// <summary>
    /// Admin purge: permanently deletes the retired demo tenants (see <see cref="DemoTenantPurgePolicy"/>).
    /// Null when a demo build is running (try again later). <paramref name="dryRun"/> only counts.
    /// </summary>
    public async Task<DemoPurgeSummary?> PurgeRetiredAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        if (!await BuildGate.WaitAsync(0, cancellationToken))
            return null;

        try
        {
            return await purger.PurgeRetiredAsync(dryRun, cancellationToken);
        }
        finally
        {
            BuildGate.Release();
        }
    }

    private async Task<bool> BuildAsync(CancellationToken cancellationToken)
    {
        if (!await BuildGate.WaitAsync(0, cancellationToken))
        {
            logger.LogInformation("Demo build skipped: another demo build is already running.");
            return false;
        }

        try
        {
            var watch = Stopwatch.StartNew();
            await accounts.EnsureViewerUserAsync(cancellationToken);

            var stamp = clock.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var ownerId = await accounts.CreateOwnerUserAsync(DemoConstants.OwnerUserName(stamp), "Demo Farm Owner", cancellationToken);
            var tenant = await store.CreateTenantAsync(ownerId, DemoConstants.OrganizationName, DemoConstants.SlugPrefix + stamp, cancellationToken);

            try
            {
                var plan = DemoSeedPlan.Create(clock.TodayFor(tenant.TenantId));
                await seeder.SeedAsync(tenant, ownerId, plan, cancellationToken);
                await store.PublishAsync(tenant.TenantId, cancellationToken);
            }
            catch
            {
                await store.DiscardAsync(tenant.TenantId, CancellationToken.None);
                throw;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Demo tenant {Slug} ({TenantId}) built and published in {Seconds:0.0}s.",
                    tenant.Slug, tenant.TenantId, watch.Elapsed.TotalSeconds);
            }

            // The new tenant is live; the ones it retired can go. A failed purge never fails the build.
            try
            {
                await purger.PurgeRetiredAsync(dryRun: false, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Purging retired demo tenants after the rebuild failed.");
            }

            return true;
        }
        finally
        {
            BuildGate.Release();
        }
    }
}
