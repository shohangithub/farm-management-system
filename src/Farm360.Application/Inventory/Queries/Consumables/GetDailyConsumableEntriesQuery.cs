using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.Consumables;

public sealed record GetDailyConsumableEntriesQuery(
    Guid FarmId,
    DateOnly EntryDate) : IRequest<IReadOnlyList<DailyConsumableEntryDto>>;

public sealed class GetDailyConsumableEntriesQueryHandler : IRequestHandler<GetDailyConsumableEntriesQuery, IReadOnlyList<DailyConsumableEntryDto>>
{
    private readonly IDailyConsumableEntryRepository _entryRepository;
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public GetDailyConsumableEntriesQueryHandler(
        IDailyConsumableEntryRepository entryRepository,
        IConsumableUsagePlanRepository planRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _entryRepository = entryRepository;
        _planRepository = planRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<IReadOnlyList<DailyConsumableEntryDto>> Handle(GetDailyConsumableEntriesQuery request, CancellationToken cancellationToken)
    {
        var entries = await _entryRepository.GetByDateAsync(request.FarmId, request.EntryDate, cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        var plans = await _planRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var planMap = plans.ToDictionary(x => x.Id);

        var items = await _inventoryItemRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var itemMap = items.ToDictionary(x => x.Id);

        return entries.Select(entry =>
        {
            planMap.TryGetValue(entry.ConsumableUsagePlanId, out var plan);
            itemMap.TryGetValue(entry.InventoryItemId, out var invItem);

            return new DailyConsumableEntryDto(
                entry.Id,
                entry.FarmId,
                entry.ConsumableUsagePlanId,
                plan?.Name ?? "Unknown Plan",
                entry.ConsumableUsagePlanItemId,
                entry.InventoryItemId,
                invItem?.Name ?? "Unknown Item",
                invItem?.Sku ?? "",
                invItem?.UnitOfMeasure ?? "",
                invItem?.CurrentStock ?? 0,
                entry.EntryDate,
                entry.ExpectedQuantity,
                entry.ActualQuantity,
                entry.UnitCostAtConsumptionBdt,
                entry.TotalCostBdt,
                entry.Status,
                entry.Status.ToString(),
                entry.AdjustmentReason,
                entry.InventoryTransactionId);
        }).ToList();
    }
}

public sealed record GetDailyConsumableSummaryQuery(
    Guid FarmId,
    DateOnly EntryDate) : IRequest<DailyConsumableSummaryDto>;

public sealed class GetDailyConsumableSummaryQueryHandler : IRequestHandler<GetDailyConsumableSummaryQuery, DailyConsumableSummaryDto>
{
    private readonly IDailyConsumableEntryRepository _entryRepository;

    public GetDailyConsumableSummaryQueryHandler(IDailyConsumableEntryRepository entryRepository)
    {
        _entryRepository = entryRepository;
    }

    public async Task<DailyConsumableSummaryDto> Handle(GetDailyConsumableSummaryQuery request, CancellationToken cancellationToken)
    {
        var entries = await _entryRepository.GetByDateAsync(request.FarmId, request.EntryDate, cancellationToken);

        var totalExpected = entries.Sum(e => e.ExpectedQuantity);
        var totalActual = entries.Where(e => e.ActualQuantity.HasValue).Sum(e => e.ActualQuantity!.Value);
        var totalCost = entries.Where(e => e.TotalCostBdt.HasValue).Sum(e => e.TotalCostBdt!.Value);

        return new DailyConsumableSummaryDto(
            request.EntryDate,
            entries.Count,
            entries.Count(e => e.Status == Domain.Inventory.Enums.DailyConsumableEntryStatus.Confirmed || e.Status == Domain.Inventory.Enums.DailyConsumableEntryStatus.Adjusted),
            entries.Count(e => e.Status == Domain.Inventory.Enums.DailyConsumableEntryStatus.Skipped),
            entries.Count(e => e.Status == Domain.Inventory.Enums.DailyConsumableEntryStatus.Pending),
            totalExpected,
            totalActual,
            totalCost);
    }
}
