using Farm360.Domain.Tenancy;

namespace Farm360.Application.Demo;

/// <summary>A tenant as the purge sees it: just what the safety rules look at.</summary>
public sealed record DemoTenantCandidate(Guid Id, string Slug, TenantStatus Status, DateTime CreatedAtUtc);

/// <summary>What a purge of one tenant removed (or, in a dry run, would remove).</summary>
public sealed record DemoTenantPurgeResult(
    Guid TenantId,
    string Slug,
    bool DryRun,
    IReadOnlyDictionary<string, long> RowsByTable,
    IReadOnlyList<string> UploadsToDelete)
{
    public long TotalRows => RowsByTable.Values.Sum();
}

/// <summary>
/// The ONE place that decides which tenants a demo purge may delete. Everything that deletes
/// (the purge after a rebuild, the admin endpoint, and the store again inside its transaction)
/// goes through <see cref="SelectPurgeable"/>.
/// </summary>
public static class DemoTenantPurgePolicy
{
    /// <summary>
    /// The tenants that may be permanently deleted. A tenant qualifies only when ALL hold:
    /// <list type="number">
    ///   <item>its slug is a demo slug (<see cref="DemoConstants.IsDemoSlug"/>, the marker the demo code
    ///         itself uses to find its tenants);</item>
    ///   <item>it is retired: status <see cref="TenantStatus.Cancelled"/>;</item>
    ///   <item>it is not the published demo tenant (the one the Viewer signs in to);</item>
    ///   <item>it is not the newest demo tenant, and it was created before the published one, so a
    ///         build in progress or anything newer than what visitors see is never touched.</item>
    /// </list>
    /// When no demo tenant is published, or the published id is not a demo tenant, nothing qualifies.
    /// </summary>
    public static IReadOnlyList<DemoTenantCandidate> SelectPurgeable(
        IEnumerable<DemoTenantCandidate> tenants, Guid? publishedTenantId)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        if (publishedTenantId is not { } publishedId || publishedId == Guid.Empty)
            return [];

        var demo = tenants.Where(t => t is not null && DemoConstants.IsDemoSlug(t.Slug)).ToList();
        var published = demo.FirstOrDefault(t => t.Id == publishedId);
        if (published is null || published.Status == TenantStatus.Cancelled)
            return [];

        var newestCreated = demo.Max(t => t.CreatedAtUtc);

        return demo
            .Where(t => t.Status == TenantStatus.Cancelled
                        && t.Id != publishedId
                        && t.Id != Guid.Empty
                        && t.CreatedAtUtc < newestCreated
                        && t.CreatedAtUtc < published.CreatedAtUtc)
            .OrderBy(t => t.CreatedAtUtc)
            .ToList();
    }
}
