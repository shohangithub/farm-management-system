using Farm360.Application.Common.Interfaces;
using Hangfire;

namespace Farm360.Infrastructure.DependencyInjection;

/// <summary>
/// Hangfire implementation of IBackgroundJobService.
/// Constitution §11: All background jobs MUST SetTenant() before processing.
/// F360-MTA-2026-001 Golden Rule §7: No implicit tenant context in background workers.
/// </summary>
public sealed class HangfireBackgroundJobService(
    IBackgroundJobClient backgroundJobClient,
    IRecurringJobManager recurringJobManager) : IBackgroundJobService
{
    public string Enqueue<T>(System.Linq.Expressions.Expression<Action<T>> job) =>
        backgroundJobClient.Enqueue(job);

    public string Schedule<T>(System.Linq.Expressions.Expression<Action<T>> job, TimeSpan delay) =>
        backgroundJobClient.Schedule(job, delay);

    public void AddOrUpdateRecurring<T>(string jobId, System.Linq.Expressions.Expression<Action<T>> job, string cronExpression) =>
        recurringJobManager.AddOrUpdate(jobId, job, cronExpression);

    public void Delete(string jobId) =>
        backgroundJobClient.Delete(jobId);
}
