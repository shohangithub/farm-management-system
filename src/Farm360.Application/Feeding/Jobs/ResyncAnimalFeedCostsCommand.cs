using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Feeding.Jobs;

public sealed record ResyncAnimalFeedCostsResult(int SkippedAllocationsZeroed, int AnimalsChecked, int LedgersUpdated);

/// <summary>
/// One-off repair for one farm of the current tenant: zeroes the allocation rows of skipped
/// entries (written at generation time and never cleared), then sets every animal ledger's feed
/// bucket to its confirmed allocation total. Idempotent — safe to run any number of times.
/// </summary>
public sealed record ResyncAnimalFeedCostsCommand(Guid FarmId) : IRequest<ResyncAnimalFeedCostsResult>;

public sealed class ResyncAnimalFeedCostsCommandHandler(
    IAnimalFeedAllocationRepository allocations,
    IAnimalCostLedgerRepository ledgers,
    ITenantService tenant,
    IUnitOfWork unitOfWork) : IRequestHandler<ResyncAnimalFeedCostsCommand, ResyncAnimalFeedCostsResult>
{
    public async Task<ResyncAnimalFeedCostsResult> Handle(ResyncAnimalFeedCostsCommand request, CancellationToken cancellationToken)
    {
        if (request.FarmId == Guid.Empty)
            throw new ArgumentException("Choose a farm.");

        var skipped = await allocations.GetNonZeroForSkippedEntriesAsync(request.FarmId, cancellationToken);
        foreach (var row in skipped)
            row.UpdateConsumption(0m, 0m, row.UnitCostBdtPerKg);
        if (skipped.Count > 0)
        {
            allocations.UpdateRange(skipped);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Every animal with a ledger on the farm (a stale bucket must be reset even to zero),
        // plus every animal fed here that has none yet.
        var animalIds = (await ledgers.GetByFarmIdAsync(request.FarmId, cancellationToken)).Select(l => l.AnimalId)
            .Concat(await allocations.GetAnimalIdsByFarmAsync(request.FarmId, cancellationToken))
            .Distinct()
            .ToList();

        var updated = await AnimalFeedCostSync.SyncAsync(allocations, ledgers, tenant.TenantId, request.FarmId, animalIds, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResyncAnimalFeedCostsResult(skipped.Count, animalIds.Count, updated);
    }
}
