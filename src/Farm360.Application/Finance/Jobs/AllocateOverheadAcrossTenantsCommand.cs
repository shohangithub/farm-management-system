using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.Jobs;

/// <summary>
/// Monthly recurring job (docs/32 GAP-2): runs <see cref="IOverheadAllocationService"/> for every
/// farm in every tenant, for the previous calendar month, so labour/overhead lands on animal cost
/// ledgers without anyone having to remember to click the manual "Allocate" action.
/// </summary>
/// <remarks>
/// Each farm belongs to exactly one tenant, and the allocation's reads are tenant-scoped (EF Core's
/// global query filter keys off <see cref="ITenantService.TenantId"/>, evaluated at query time —
/// see ApplicationDbContext). So the loop sets the ambient tenant before each farm's run, the same
/// concern <see cref="Farm360.Identity.Services.IdentityServices"/>'s per-request resolution handles
/// for an HTTP call. The per-farm allocation itself is unchanged and already idempotent: re-running
/// a period, or a farm with nothing new to allocate, is a no-op.
/// </remarks>
public sealed record AllocateOverheadAcrossTenantsCommand : IRequest;

public sealed class AllocateOverheadAcrossTenantsCommandHandler : IRequestHandler<AllocateOverheadAcrossTenantsCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly ITenantService _tenantService;
    private readonly IOverheadAllocationService _allocationService;
    private readonly ILogger<AllocateOverheadAcrossTenantsCommandHandler> _logger;

    public AllocateOverheadAcrossTenantsCommandHandler(
        ITenantRepository tenantRepository,
        IFarmRepository farmRepository,
        ITenantService tenantService,
        IOverheadAllocationService allocationService,
        ILogger<AllocateOverheadAcrossTenantsCommandHandler> logger)
    {
        _tenantRepository = tenantRepository;
        _farmRepository = farmRepository;
        _tenantService = tenantService;
        _allocationService = allocationService;
        _logger = logger;
    }

    public async Task Handle(AllocateOverheadAcrossTenantsCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        var from = firstOfThisMonth.AddMonths(-1);
        var to = firstOfThisMonth.AddDays(-1);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Running recurring background job: AllocateOverheadAcrossTenants for {From}..{To}", from, to);
        }

        var tenants = await _tenantRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var farmsProcessed = 0;
        var farmsFailed = 0;
        var totalAmountAllocated = 0m;

        foreach (var tenant in tenants)
        {
            // Mirrors per-request tenant resolution: the allocation's repository reads are scoped
            // by this value, evaluated at query time, not by the farmId argument alone.
            _tenantService.SetTenant(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                tenant.SubscriptionTier.ToString(),
                tenant.Status.ToString());

            IReadOnlyList<Farm360.Domain.Farms.Farm> farms;
#pragma warning disable CA1031 // One tenant's failure must not abort the run for every other tenant.
            try
            {
                farms = await _farmRepository.GetAllByTenantAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list farms for tenant {TenantId} during overhead allocation.", tenant.Id);
                continue;
            }
#pragma warning restore CA1031

#pragma warning disable CA1031 // One farm's failure must not abort the run for every other farm.
            foreach (var farm in farms)
            {
                try
                {
                    var result = await _allocationService
                        .AllocateAsync(farm.Id, from, to, isBackfill: false, cancellationToken)
                        .ConfigureAwait(false);

                    farmsProcessed++;
                    totalAmountAllocated += result.AmountAllocatedBdt;
                }
                catch (Exception ex)
                {
                    farmsFailed++;
                    _logger.LogError(ex,
                        "Overhead allocation failed for farm {FarmId} (tenant {TenantId}), period {From}..{To}.",
                        farm.Id, tenant.Id, from, to);
                }
            }
#pragma warning restore CA1031
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Overhead allocation across tenants complete: {FarmsProcessed} farms processed, {FarmsFailed} failed, {Amount} BDT allocated.",
                farmsProcessed, farmsFailed, totalAmountAllocated);
        }
    }
}
