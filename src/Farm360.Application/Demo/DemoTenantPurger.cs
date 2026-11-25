using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Demo;

/// <summary>Outcome of one purge run.</summary>
public sealed record DemoPurgeSummary(
    bool DryRun,
    IReadOnlyList<DemoTenantPurgeResult> Purged,
    IReadOnlyList<DemoPurgeFailure> Failed);

public sealed record DemoPurgeFailure(Guid TenantId, string Slug, string Error);

/// <summary>
/// Permanently deletes RETIRED demo tenants (each weekly rebuild leaves one behind). Which tenants
/// qualify is decided only by <see cref="DemoTenantPurgePolicy"/>; each tenant is deleted in its own
/// transaction by <see cref="IDemoTenantStore.PurgeTenantAsync"/>, so a failure leaves that tenant
/// whole and the others still go. Uploaded files are removed only after their tenant's rows are gone.
/// </summary>
public sealed class DemoTenantPurger(
    IDemoTenantStore store,
    IFileStorageService files,
    ILogger<DemoTenantPurger> logger)
{
    public async Task<DemoPurgeSummary> PurgeRetiredAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var candidates = await store.ListDemoTenantsAsync(cancellationToken);
        var published = await store.GetPublishedTenantIdAsync(cancellationToken);
        var targets = DemoTenantPurgePolicy.SelectPurgeable(candidates, published);

        var purged = new List<DemoTenantPurgeResult>();
        var failed = new List<DemoPurgeFailure>();

        foreach (var target in targets)
        {
            DemoTenantPurgeResult result;
            try
            {
                result = await store.PurgeTenantAsync(target.Id, dryRun, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Purge of retired demo tenant {Slug} ({TenantId}) failed and was rolled back.", target.Slug, target.Id);
                failed.Add(new DemoPurgeFailure(target.Id, target.Slug, ex.Message));
                continue;
            }

            purged.Add(result);
            if (dryRun)
                continue;

            foreach (var url in result.UploadsToDelete)
            {
                try
                {
                    await files.DeleteFileAsync(url, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not delete upload {Url} of purged demo tenant {TenantId}.", url, target.Id);
                }
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Purged retired demo tenant {Slug} ({TenantId}): {Rows} rows, {Files} uploaded files.",
                    result.Slug, result.TenantId, result.TotalRows, result.UploadsToDelete.Count);
            }
        }

        return new DemoPurgeSummary(dryRun, purged, failed);
    }
}
