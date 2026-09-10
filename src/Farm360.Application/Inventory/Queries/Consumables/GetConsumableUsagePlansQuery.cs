using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.Consumables;

public sealed record GetConsumableUsagePlansQuery(
    Guid FarmId,
    int PageNumber = 1,
    int PageSize = 20,
    ConsumableUsagePlanStatus? Status = null,
    string? SearchTerm = null) : IRequest<PagedResult<ConsumableUsagePlanDto>>;

public sealed class GetConsumableUsagePlansQueryHandler : IRequestHandler<GetConsumableUsagePlansQuery, PagedResult<ConsumableUsagePlanDto>>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public GetConsumableUsagePlansQueryHandler(
        IConsumableUsagePlanRepository planRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _planRepository = planRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<PagedResult<ConsumableUsagePlanDto>> Handle(GetConsumableUsagePlansQuery request, CancellationToken cancellationToken)
    {
        var (plans, totalCount) = await _planRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.FarmId,
            request.Status,
            request.SearchTerm,
            cancellationToken);

        var farmItems = await _inventoryItemRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var itemMap = farmItems.ToDictionary(x => x.Id);

        var planDtos = plans.Select(plan =>
        {
            var itemDtos = plan.Items.Select(item =>
            {
                itemMap.TryGetValue(item.InventoryItemId, out var invItem);
                return new ConsumableUsagePlanItemDto(
                    item.Id,
                    item.ConsumableUsagePlanId,
                    item.InventoryItemId,
                    invItem?.Name ?? "Unknown Item",
                    invItem?.Sku ?? "",
                    invItem?.UnitOfMeasure ?? "",
                    invItem?.CurrentStock ?? 0,
                    invItem?.WeightedAverageCostBdt ?? 0,
                    item.PlannedQuantityPerDay,
                    item.Notes);
            }).ToList();

            var totalDailyCost = itemDtos.Sum(i => i.PlannedQuantityPerDay * i.WeightedAverageCostBdt);

            return new ConsumableUsagePlanDto(
                plan.Id,
                plan.FarmId,
                plan.Name,
                plan.Description,
                plan.Status,
                plan.Status.ToString(),
                plan.StartDate,
                plan.EndDate,
                plan.Items.Count,
                totalDailyCost,
                itemDtos);
        }).ToList();

        return new PagedResult<ConsumableUsagePlanDto>(
            planDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}

public sealed record GetConsumableUsagePlanByIdQuery(Guid Id) : IRequest<ConsumableUsagePlanDto>;

public sealed class GetConsumableUsagePlanByIdQueryHandler : IRequestHandler<GetConsumableUsagePlanByIdQuery, ConsumableUsagePlanDto>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public GetConsumableUsagePlanByIdQueryHandler(
        IConsumableUsagePlanRepository planRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _planRepository = planRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<ConsumableUsagePlanDto> Handle(GetConsumableUsagePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetByIdAsync(request.Id, includeItems: true, cancellationToken)
            ?? throw new NotFoundException(nameof(ConsumableUsagePlan), request.Id);

        var farmItems = await _inventoryItemRepository.GetByFarmIdAsync(plan.FarmId, cancellationToken: cancellationToken);
        var itemMap = farmItems.ToDictionary(x => x.Id);

        var itemDtos = plan.Items.Select(item =>
        {
            itemMap.TryGetValue(item.InventoryItemId, out var invItem);
            return new ConsumableUsagePlanItemDto(
                item.Id,
                item.ConsumableUsagePlanId,
                item.InventoryItemId,
                invItem?.Name ?? "Unknown Item",
                invItem?.Sku ?? "",
                invItem?.UnitOfMeasure ?? "",
                invItem?.CurrentStock ?? 0,
                invItem?.WeightedAverageCostBdt ?? 0,
                item.PlannedQuantityPerDay,
                item.Notes);
        }).ToList();

        var totalDailyCost = itemDtos.Sum(i => i.PlannedQuantityPerDay * i.WeightedAverageCostBdt);

        return new ConsumableUsagePlanDto(
            plan.Id,
            plan.FarmId,
            plan.Name,
            plan.Description,
            plan.Status,
            plan.Status.ToString(),
            plan.StartDate,
            plan.EndDate,
            plan.Items.Count,
            totalDailyCost,
            itemDtos);
    }
}
