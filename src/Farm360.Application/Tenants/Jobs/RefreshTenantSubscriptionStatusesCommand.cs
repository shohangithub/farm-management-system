using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Tenants.Jobs;

/// <summary>
/// Daily recurring job: evaluates every tenant's trial/subscription expiry and transitions its
/// status (Active to GracePeriod to Suspended) accordingly.
/// </summary>
/// <remarks>
/// Before this job, <see cref="Farm360.Domain.Tenancy.Tenant.CheckAndRefreshStatus"/> only ran
/// reactively, inside the query the billing page calls on load. A tenant that never opened that
/// page after its subscription lapsed would keep full access indefinitely, since the API's
/// Suspended gate (TenantResolutionMiddleware) only reacts to a status that is actually Suspended
/// in the database -- nothing proactively put it there. This job closes that gap.
/// </remarks>
public sealed record RefreshTenantSubscriptionStatusesCommand : IRequest;

public sealed class RefreshTenantSubscriptionStatusesCommandHandler
    : IRequestHandler<RefreshTenantSubscriptionStatusesCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RefreshTenantSubscriptionStatusesCommandHandler> _logger;

    public RefreshTenantSubscriptionStatusesCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<RefreshTenantSubscriptionStatusesCommandHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Handle(RefreshTenantSubscriptionStatusesCommand request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Running recurring background job: RefreshTenantSubscriptionStatuses");
        }

        var tenants = await _tenantRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var transitioned = 0;

        foreach (var tenant in tenants)
        {
            var oldStatus = tenant.Status;
            var newStatus = tenant.CheckAndRefreshStatus();

            if (oldStatus == newStatus)
            {
                continue;
            }

            _tenantRepository.Update(tenant);
            transitioned++;

            // The API's tenant-resolution middleware caches status per tenant; a stale cached
            // "Active" would let a just-suspended tenant keep working until the cache expired.
            await _cacheService.RemoveAsync($"tenant:{tenant.Id}:context", cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(tenant.Slug))
            {
                await _cacheService.RemoveAsync($"tenant:slug:{tenant.Slug}:context", cancellationToken).ConfigureAwait(false);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Tenant {TenantId} ({Slug}) status transitioned {OldStatus} -> {NewStatus}.",
                    tenant.Id, tenant.Slug, oldStatus, newStatus);
            }
        }

        if (transitioned > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Tenant subscription status refresh complete: {Transitioned} of {Total} tenants transitioned.",
                transitioned, tenants.Count);
        }
    }
}
