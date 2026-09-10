using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.Jobs;

public sealed record CreateDailyConsumableEntriesAcrossTenantsCommand : IRequest;

public sealed class CreateDailyConsumableEntriesAcrossTenantsCommandHandler : IRequestHandler<CreateDailyConsumableEntriesAcrossTenantsCommand>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDailyConsumableEntriesAcrossTenantsCommandHandler> _logger;

    public CreateDailyConsumableEntriesAcrossTenantsCommandHandler(
        IConsumableUsagePlanRepository planRepository,
        IDailyConsumableEntryRepository entryRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateDailyConsumableEntriesAcrossTenantsCommandHandler> logger)
    {
        _planRepository = planRepository;
        _entryRepository = entryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CreateDailyConsumableEntriesAcrossTenantsCommand request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Running recurring background job: CreateDailyConsumableEntriesAcrossTenants");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activePlans = await _planRepository.GetAllActivePlansAcrossTenantsAsync(today, cancellationToken);

        if (activePlans.Count == 0)
        {
            return;
        }

        var existingPlanItemIds = await _entryRepository.GetExistingEntryPlanItemIdsAcrossTenantsByDateAsync(today, cancellationToken);
        var entriesToCreate = new List<DailyConsumableEntry>();

        foreach (var plan in activePlans)
        {
            foreach (var item in plan.Items)
            {
                if (existingPlanItemIds.Contains((plan.Id, item.Id)))
                {
                    continue; // Already exists for today
                }

                var entry = new DailyConsumableEntry(
                    id: Guid.NewGuid(),
                    tenantId: plan.TenantId,
                    farmId: plan.FarmId,
                    consumableUsagePlanId: plan.Id,
                    consumableUsagePlanItemId: item.Id,
                    inventoryItemId: item.InventoryItemId,
                    entryDate: today,
                    expectedQuantity: item.PlannedQuantityPerDay);

                entriesToCreate.Add(entry);
            }
        }

        if (entriesToCreate.Count > 0)
        {
            await _entryRepository.AddRangeAsync(entriesToCreate, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Auto-generated {Count} daily consumable entries across all tenants for {Date}", entriesToCreate.Count, today);
            }
        }
    }
}
