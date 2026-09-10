using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.Commands.Consumables;

public sealed record GenerateDailyConsumableEntriesCommand(Guid FarmId, DateOnly? TargetDate = null) : IRequest<int>;

public sealed class GenerateDailyConsumableEntriesCommandHandler : IRequestHandler<GenerateDailyConsumableEntriesCommand, int>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateDailyConsumableEntriesCommandHandler> _logger;

    public GenerateDailyConsumableEntriesCommandHandler(
        IConsumableUsagePlanRepository planRepository,
        IDailyConsumableEntryRepository entryRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        ILogger<GenerateDailyConsumableEntriesCommandHandler> logger)
    {
        _planRepository = planRepository;
        _entryRepository = entryRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<int> Handle(GenerateDailyConsumableEntriesCommand request, CancellationToken cancellationToken)
    {
        var targetDate = request.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var activePlans = await _planRepository.GetActivePlansAsync(request.FarmId, targetDate, cancellationToken);

        if (activePlans.Count == 0)
        {
            return 0;
        }

        var existingPlanItemIds = await _entryRepository.GetExistingEntryPlanItemIdsByDateAsync(request.FarmId, targetDate, cancellationToken);
        var entriesToCreate = new List<DailyConsumableEntry>();

        foreach (var plan in activePlans)
        {
            foreach (var item in plan.Items)
            {
                if (existingPlanItemIds.Contains((plan.Id, item.Id)))
                {
                    continue; // Already generated for this day
                }

                var entry = new DailyConsumableEntry(
                    id: Guid.NewGuid(),
                    tenantId: _tenantService.TenantId,
                    farmId: request.FarmId,
                    consumableUsagePlanId: plan.Id,
                    consumableUsagePlanItemId: item.Id,
                    inventoryItemId: item.InventoryItemId,
                    entryDate: targetDate,
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
                _logger.LogInformation("Generated {Count} daily consumable entries for Farm {FarmId} on {Date}", entriesToCreate.Count, request.FarmId, targetDate);
            }
        }

        return entriesToCreate.Count;
    }
}
