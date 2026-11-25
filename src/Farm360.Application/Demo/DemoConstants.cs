namespace Farm360.Application.Demo;

/// <summary>
/// Fixed facts about the public "Try the demo" farm. The demo Viewer account has a fixed id so the
/// demo login can only ever issue a session for that one account, and so the API can recognise it
/// (read-only guard, per-IP rate limiting, <c>isDemo</c> on the profile) without a database lookup.
/// </summary>
public static class DemoConstants
{
    /// <summary>The shared, read-only demo account every prospect signs in as.</summary>
    public static readonly Guid ViewerUserId = new("d3e70000-0000-4000-8000-000000000d30");

    /// <summary>User name of the demo Viewer. Not a phone number, so it is never offered at the normal sign-in.</summary>
    public const string ViewerUserName = "demo.viewer@farm360.demo";

    public const string ViewerFullName = "Demo Visitor";

    /// <summary>Every demo tenant's slug starts with this; a rebuild retires the older ones.</summary>
    public const string SlugPrefix = "farm360-demo-";

    public const string OrganizationName = "Shapla Agro Farm (Demo)";

    public const string BranchName = "Savar Branch";

    public const string FarmName = "Demo Fattening Farm";

    /// <summary>Shown when the demo is switched off or its data is still being built.</summary>
    public const string NotReadyMessage = "The demo is being prepared, please try again in a minute.";

    /// <summary>Hangfire recurring job id of the weekly rebuild.</summary>
    public const string WeeklyRebuildJobId = "demo-tenant-weekly-rebuild";

    /// <summary>User name of each build's throwaway Owner: <c>demo.owner.{stamp}@farm360.demo</c>.</summary>
    public const string OwnerUserNamePrefix = "demo.owner.";

    /// <summary>Domain every demo account's user name ends with. Not a real mail domain.</summary>
    public const string UserNameDomain = "@farm360.demo";

    public static bool IsDemoViewer(Guid? userId) => userId == ViewerUserId;

    /// <summary>True for a slug made by a demo build (case-sensitive prefix plus a non-empty stamp).</summary>
    public static bool IsDemoSlug(string? slug) =>
        slug is not null && slug.Length > SlugPrefix.Length && slug.StartsWith(SlugPrefix, StringComparison.Ordinal);

    /// <summary>True for the user name of a build's throwaway Owner (never the Viewer's).</summary>
    public static bool IsDemoOwnerUserName(string? userName) =>
        userName is not null
        && userName.Length > OwnerUserNamePrefix.Length + UserNameDomain.Length
        && userName.StartsWith(OwnerUserNamePrefix, StringComparison.Ordinal)
        && userName.EndsWith(UserNameDomain, StringComparison.Ordinal);

    /// <summary>User name of the Owner a build started at <paramref name="stamp"/> runs as.</summary>
    public static string OwnerUserName(string stamp) => OwnerUserNamePrefix + stamp + UserNameDomain;
}

/// <summary>Configuration section <c>Demo</c>.</summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>
    /// Turns the public demo on: the demo tenant is seeded in the background after start-up, rebuilt
    /// weekly, and the sign-in page offers "Explore the demo farm". Default false.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>Public answer of <c>GET /api/v1/auth/demo-status</c>.</summary>
public sealed record DemoStatusDto(bool Enabled, bool Ready);

/// <summary>The demo tenant created for a build: its ids and the names the request context needs.</summary>
public sealed record DemoTenantInfo(Guid TenantId, Guid OrganizationId, string Slug, string Name, string Tier);

/// <summary>
/// Identity side of the demo (implemented in Farm360.Identity): the shared Viewer account and the
/// throwaway Owner account each build runs as.
/// </summary>
public interface IDemoAccountService
{
    /// <summary>Creates the demo Viewer account if it does not exist yet. Its password is random and never stored.</summary>
    Task EnsureViewerUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the Owner account a build acts as. Its password is a random GUID that is never stored
    /// or shown, so nobody can sign in as it.
    /// </summary>
    Task<Guid> CreateOwnerUserAsync(string userName, string fullName, CancellationToken cancellationToken = default);

    /// <summary>
    /// True only when the demo Viewer account exists exactly as created: the fixed id, the demo user
    /// name and not a platform administrator. The demo login refuses otherwise.
    /// </summary>
    Task<bool> IsSafeViewerAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant side of the demo (implemented in Farm360.Persistence): creating the tenant through the
/// sign-up path, publishing it to the Viewer, and retiring older demo tenants.
/// </summary>
public interface IDemoTenantStore
{
    /// <summary>Creates the tenant, its organization and the owner membership (the sign-up path), on a plan that never expires.</summary>
    Task<DemoTenantInfo> CreateTenantAsync(Guid ownerUserId, string organizationName, string slug, CancellationToken cancellationToken = default);

    /// <summary>The newest demo tenant the Viewer can sign in to, or null while none is ready.</summary>
    Task<Guid?> GetPublishedTenantIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Gives the Viewer access to <paramref name="tenantId"/> and retires every other demo tenant.</summary>
    Task PublishAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Retires a tenant whose build failed half way.</summary>
    Task DiscardAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every tenant whose slug looks like a demo slug, retired or not (soft-deleted ones included).
    /// The caller still filters with <see cref="DemoTenantPurgePolicy"/>.
    /// </summary>
    Task<IReadOnlyList<DemoTenantCandidate>> ListDemoTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a retired demo tenant: every tenant-scoped row, its memberships and roles,
    /// its throwaway Owner account and the tenant row, in ONE transaction (all or nothing). Before
    /// deleting anything it re-checks, inside that transaction, that <see cref="DemoTenantPurgePolicy"/>
    /// still selects the tenant, and throws otherwise. With <paramref name="dryRun"/> it only counts.
    /// </summary>
    Task<DemoTenantPurgeResult> PurgeTenantAsync(Guid tenantId, bool dryRun, CancellationToken cancellationToken = default);
}

/// <summary>
/// Identity side of the purge (implemented in Farm360.Identity). Works inside the caller's
/// database transaction, so the account rows go together with the tenant's data or not at all.
/// </summary>
public interface IDemoAccountCleanup
{
    /// <summary>
    /// Of <paramref name="userIds"/>, the ones that exist and are neither the demo Viewer nor a
    /// build's throwaway Owner. A tenant with such a member is never purged.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> FindNonDemoUsersAsync(
        IReadOnlyCollection<Guid> userIds, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the throwaway Owner accounts among <paramref name="ownerUserIds"/> (re-checked: demo
    /// Owner user name, not a system user, never the Viewer) with their claims, logins, tokens,
    /// roles, sessions and devices, plus any session issued for <paramref name="tenantId"/>.
    /// Returns rows deleted per table. With <paramref name="dryRun"/> it only counts.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> DeleteDemoOwnersAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ownerUserIds, System.Data.Common.DbTransaction transaction,
        bool dryRun, CancellationToken cancellationToken = default);
}
