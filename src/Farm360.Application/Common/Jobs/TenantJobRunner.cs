using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Common.Jobs;

/// <summary>One tenant's slice of a recurring job: its id, and a sender/services bound to its own scope.</summary>
public sealed record TenantJobScope(Guid TenantId, string TenantName, ISender Sender, IServiceProvider Services);

/// <summary>How a fan-out run went.</summary>
public sealed record TenantJobSummary(int Succeeded, int Failed, IReadOnlyList<Guid> FailedTenantIds);

/// <summary>
/// Runs a recurring job once per tenant, each in its own DI scope (own DbContext / change tracker,
/// ambient tenant set as for an HTTP request), so one tenant's bad data cannot fail — or leave
/// half-written changes for — every other tenant. Failures are logged and counted, never rethrown.
/// Constitution §11: background jobs set the tenant explicitly.
/// </summary>
public interface ITenantJobRunner
{
    Task<TenantJobSummary> ForEachTenantAsync(
        string jobName,
        Func<TenantJobScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITenantJobRunner"/>
public sealed class TenantJobRunner(IServiceScopeFactory scopeFactory, ILogger<TenantJobRunner> logger) : ITenantJobRunner
{
    public async Task<TenantJobSummary> ForEachTenantAsync(
        string jobName,
        Func<TenantJobScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        IReadOnlyList<Domain.Tenancy.Tenant> tenants;
        await using (var listScope = scopeFactory.CreateAsyncScope())
        {
            tenants = await listScope.ServiceProvider.GetRequiredService<ITenantRepository>()
                .GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        var succeeded = 0;
        var failed = new List<Guid>();

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            // Mirrors per-request tenant resolution: EF's tenant filter reads this at query time.
            services.GetRequiredService<ITenantService>().SetTenant(
                tenant.Id, tenant.Slug, tenant.Name, tenant.SubscriptionTier.ToString(), tenant.Status.ToString());

            try
            {
                await work(new TenantJobScope(tenant.Id, tenant.Name, services.GetRequiredService<ISender>(), services), cancellationToken)
                    .ConfigureAwait(false);
                succeeded++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One tenant's failure must not abort the run for every other tenant.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failed.Add(tenant.Id);
                logger.LogError(ex, "Recurring job {JobName} failed for tenant {TenantId} ({TenantName}); other tenants continue.",
                    jobName, tenant.Id, tenant.Name);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Recurring job {JobName} finished: {Succeeded} tenants succeeded, {Failed} failed.",
                jobName, succeeded, failed.Count);
        }

        return new TenantJobSummary(succeeded, failed.Count, failed);
    }
}

/// <summary>Database error classification without referencing EF Core from Application.</summary>
public static class DatabaseErrors
{
    /// <summary>
    /// True for a unique index / primary key violation anywhere in the exception chain (SQL Server
    /// 2601 "Cannot insert duplicate key row…" / 2627 "Violation of UNIQUE KEY constraint…").
    /// Used to treat "a concurrent run already generated this day" as success, not failure.
    /// </summary>
    public static bool IsUniqueViolation(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            var message = ex.Message;
            if (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique index", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
