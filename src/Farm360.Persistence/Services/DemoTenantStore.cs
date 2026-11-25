using System.Globalization;
using System.Runtime.CompilerServices;
using Farm360.Application.Demo;
using Farm360.Domain.Identity;
using Farm360.Domain.Tenancy;
using Farm360.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Farm360.Persistence.Services;

/// <inheritdoc cref="IDemoTenantStore"/>
/// <remarks>
/// Demo tenants are recognised by their slug prefix; the one the Viewer holds an active membership
/// in is the published one. Queries bypass the tenant filter on purpose (this runs outside any
/// tenant) and are always narrowed to demo slugs or to the fixed Viewer account.
/// </remarks>
public sealed class DemoTenantStore(
    ApplicationDbContext dbContext,
    TenantMembershipService memberships,
    IDemoAccountCleanup accountCleanup) : IDemoTenantStore
{
    /// <summary>SQL Server error number of a foreign key violation.</summary>
    private const int ForeignKeyViolation = 547;

    private static readonly ConditionalWeakTable<IModel, TenantDeletePlan> Plans = new();

    public async Task<DemoTenantInfo> CreateTenantAsync(Guid ownerUserId, string organizationName, string slug, CancellationToken cancellationToken = default)
    {
        if (!slug.StartsWith(DemoConstants.SlugPrefix, StringComparison.Ordinal))
            throw new ArgumentException("A demo tenant slug must start with the demo prefix.", nameof(slug));

        // The sign-up path: tenant, owner membership and organization (BDT, Asia/Dhaka) in one go.
        var tenantId = await memberships.CreateTenantForUserAsync(
            ownerUserId, organizationName, slug, "info@shapla-agro.demo", "+8801700000100", 7, cancellationToken);

        // A demo must never expire or ask for payment: a lifetime Professional plan instead of the trial.
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId, cancellationToken);
        tenant.Subscribe(SubscriptionTier.Professional, SubscriptionBillingCycle.OneTime);
        tenant.UpdateBranding(null, "#1A7F4B", "Asia/Dhaka");
        await dbContext.SaveChangesAsync(cancellationToken);

        var organizationId = await dbContext.Organizations.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .Select(o => o.Id)
            .FirstAsync(cancellationToken);

        return new DemoTenantInfo(tenantId, organizationId, tenant.Slug, tenant.Name, tenant.SubscriptionTier.ToString());
    }

    public async Task<Guid?> GetPublishedTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var prefix = DemoConstants.SlugPrefix;
        return await (
                from tu in dbContext.TenantUsers.IgnoreQueryFilters()
                join t in dbContext.Tenants.IgnoreQueryFilters() on tu.TenantId equals t.Id
                where tu.UserId == DemoConstants.ViewerUserId
                      && tu.Status == TenantUserStatus.Active
                      && !tu.IsDeleted
                      && !t.IsDeleted
                      && t.Status == TenantStatus.Active
                      && t.Slug.StartsWith(prefix)
                orderby t.CreatedAtUtc descending
                select (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task PublishAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenant.Slug.StartsWith(DemoConstants.SlugPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Only a demo tenant can be published to the demo Viewer.");

        // The Viewer: read-only (Viewer role), every farm of the new tenant, nothing anywhere else.
        var viewerMemberships = await dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(tu => tu.UserId == DemoConstants.ViewerUserId && !tu.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var membership in viewerMemberships.Where(m => m.TenantId != tenantId && m.Status == TenantUserStatus.Active))
            membership.Deactivate();

        if (!viewerMemberships.Any(m => m.TenantId == tenantId))
            dbContext.TenantUsers.Add(TenantUser.Join(tenantId, DemoConstants.ViewerUserId, SystemRoleIds.Viewer, branchId: null));

        // Retire the older demo tenants (cancelled = soft-deleted, unreachable). Data is kept, never hard-deleted.
        var prefix = DemoConstants.SlugPrefix;
        var older = await dbContext.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id != tenantId && t.Slug.StartsWith(prefix) && t.Status != TenantStatus.Cancelled)
            .ToListAsync(cancellationToken);
        foreach (var old in older)
            old.Cancel();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DiscardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null || !tenant.Slug.StartsWith(DemoConstants.SlugPrefix, StringComparison.Ordinal))
            return;

        // The failed build may have left tracked changes behind; start from a clean context.
        dbContext.ChangeTracker.Clear();
        tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId, cancellationToken);
        tenant.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemoTenantCandidate>> ListDemoTenantsAsync(CancellationToken cancellationToken = default)
    {
        var prefix = DemoConstants.SlugPrefix;
        return await dbContext.Tenants.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Slug.StartsWith(prefix))
            .Select(t => new DemoTenantCandidate(t.Id, t.Slug, t.Status, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<DemoTenantPurgeResult> PurgeTenantAsync(Guid tenantId, bool dryRun, CancellationToken cancellationToken = default)
    {
        var plan = Plans.GetValue(dbContext.Model, TenantDeletePlan.Build);
        dbContext.ChangeTracker.Clear();

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        try
        {
            // The retrying execution strategy needs the whole transaction as one unit it can re-run.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var (slug, rows, urls) = await strategy.ExecuteAsync(
                () => PurgeInTransactionAsync(plan, tenantId, dryRun, cancellationToken));

            // Files cannot roll back, so they go only after the commit, and only the ones nothing
            // left in the database still points at (e.g. a file shared with another tenant).
            var uploads = dryRun || urls.Count == 0
                ? urls
                : urls.Except(await FindReferencedUploadsAsync(plan, urls, cancellationToken), StringComparer.Ordinal).ToList();

            return new DemoTenantPurgeResult(tenantId, slug, dryRun, rows, uploads);
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(previousTimeout);
            dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Rows the tenant has in every table the purge covers. Read-only: no safety check, nothing is
    /// deleted. Used to verify a purge from the outside.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, long>> CountTenantRowsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var plan = Plans.GetValue(dbContext.Model, TenantDeletePlan.Build);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var step in plan.Steps)
            counts[step.Table] = await CountAsync(step.CountSql, tenantId, cancellationToken);
        counts["app.Tenants"] = await dbContext.Tenants.IgnoreQueryFilters().LongCountAsync(t => t.Id == tenantId, cancellationToken);
        return counts;
    }

    private async Task<(string Slug, IReadOnlyDictionary<string, long> Rows, IReadOnlyList<string> Urls)> PurgeInTransactionAsync(
        TenantDeletePlan plan, Guid tenantId, bool dryRun, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 1. Safety, re-checked inside the transaction with the one policy: a demo slug, retired,
        //    not the published demo tenant, not the newest.
        var candidates = await ListDemoTenantsAsync(cancellationToken);
        var published = await GetPublishedTenantIdAsync(cancellationToken);
        var target = DemoTenantPurgePolicy.SelectPurgeable(candidates, published).FirstOrDefault(t => t.Id == tenantId)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is not a retired demo tenant that may be purged.");

        // 2. Every member must be a demo account (the shared Viewer or a build's throwaway Owner).
        var dbTransaction = transaction.GetDbTransaction();
        var memberIds = await dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(tu => tu.TenantId == tenantId)
            .Select(tu => tu.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var strangers = await accountCleanup.FindNonDemoUsersAsync(memberIds, dbTransaction, cancellationToken);
        if (strangers.Count > 0)
            throw new InvalidOperationException($"Demo tenant {target.Slug} has {strangers.Count} member(s) that are not demo accounts; it was left untouched.");

        // An Owner account that is also a member of another tenant is kept (only this membership goes).
        // The shared Viewer is never deleted: every rebuild reuses it.
        var nonViewers = memberIds.Where(id => id != DemoConstants.ViewerUserId).ToList();
        var usedElsewhere = await dbContext.TenantUsers.IgnoreQueryFilters()
            .Where(tu => nonViewers.Contains(tu.UserId) && tu.TenantId != tenantId)
            .Select(tu => tu.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var ownerIds = nonViewers.Except(usedElsewhere).ToList();

        // 3. Uploaded files the tenant's rows point at (collected before the rows go).
        var urls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in plan.Steps.Where(s => s.UploadUrlSql is not null))
        {
            urls.UnionWith(await dbContext.Database
                .SqlQueryRaw<string>(step.UploadUrlSql!, TenantParameter(tenantId))
                .ToListAsync(cancellationToken));
        }

        var logo = await dbContext.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.LogoUrl)
            .FirstOrDefaultAsync(cancellationToken);
        if (logo is not null
            && (logo.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) || logo.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)))
        {
            urls.Add(logo);
        }

        // 4. The tenant's rows, dependents first.
        var rows = new Dictionary<string, long>(StringComparer.Ordinal);
        if (dryRun)
        {
            foreach (var step in plan.Steps)
                rows[step.Table] = await CountAsync(step.CountSql, tenantId, cancellationToken);
        }
        else
        {
            await DeleteRowsAsync(plan, tenantId, rows, cancellationToken);
        }

        // 5. The throwaway Owner accounts, in the same transaction.
        foreach (var (table, count) in await accountCleanup.DeleteDemoOwnersAsync(tenantId, ownerIds, dbTransaction, dryRun, cancellationToken))
            rows[table] = count;

        // 6. The tenant row itself, guarded again by id, status and slug.
        var prefix = DemoConstants.SlugPrefix;
        var tenantRow = dbContext.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId && t.Status == TenantStatus.Cancelled && t.Slug.StartsWith(prefix));
        long tenantRows = dryRun
            ? await tenantRow.LongCountAsync(cancellationToken)
            : await tenantRow.ExecuteDeleteAsync(cancellationToken);
        if (tenantRows != 1)
            throw new InvalidOperationException($"Expected exactly one tenant row for {target.Slug}, found {tenantRows}.");
        rows["app.Tenants"] = tenantRows;

        if (dryRun)
            await transaction.RollbackAsync(cancellationToken);
        else
            await transaction.CommitAsync(cancellationToken);

        return (target.Slug, rows, urls.OrderBy(u => u, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Runs the plan's deletes. A delete blocked by a foreign key (a cycle the ordering could not
    /// break) is retried after the others; once a whole pass makes no progress the purge throws and
    /// the caller's transaction rolls back.
    /// </summary>
    private async Task DeleteRowsAsync(TenantDeletePlan plan, Guid tenantId, Dictionary<string, long> rows, CancellationToken cancellationToken)
    {
        foreach (var step in plan.Steps)
            rows[step.Table] = 0;

        var pending = plan.Steps.ToList();
        while (pending.Count > 0)
        {
            var blocked = new List<TenantDeletePlan.Step>();
            SqlException? lastError = null;
            foreach (var step in pending)
            {
                try
                {
                    rows[step.Table] += await dbContext.Database.ExecuteSqlRawAsync(step.DeleteSql, [TenantParameter(tenantId)], cancellationToken);
                }
                catch (SqlException ex) when (ex.Number == ForeignKeyViolation)
                {
                    blocked.Add(step);
                    lastError = ex;
                }
            }

            if (blocked.Count == pending.Count)
            {
                throw new InvalidOperationException(
                    "Could not delete the tenant's rows in " + string.Join(", ", blocked.Select(b => b.Table)) + " (still referenced).",
                    lastError);
            }

            pending = blocked;
        }
    }

    private async Task<long> CountAsync(string sql, Guid tenantId, CancellationToken cancellationToken) =>
        (await dbContext.Database.SqlQueryRaw<long>(sql, TenantParameter(tenantId)).ToListAsync(cancellationToken)).Single();

    /// <summary>Of <paramref name="urls"/>, those some row anywhere in the database still holds.</summary>
    private async Task<HashSet<string>> FindReferencedUploadsAsync(TenantDeletePlan plan, IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        var names = urls.Select((_, i) => "@u" + i.ToString(CultureInfo.InvariantCulture)).ToList();
        var inList = "(" + string.Join(", ", names) + ")";

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scan in plan.TextScans)
        {
            // Only model-derived names and parameter placeholders are concatenated; the URLs are parameters.
            var sql = scan + inList;
            var parameters = names.Select((n, i) => (object)new SqlParameter(n, urls[i])).ToArray();
            referenced.UnionWith(await dbContext.Database.SqlQueryRaw<string>(sql, parameters).ToListAsync(cancellationToken));
        }

        return referenced;
    }

    private static SqlParameter TenantParameter(Guid tenantId) => new(TenantDeletePlan.TenantIdParameter, tenantId);
}
