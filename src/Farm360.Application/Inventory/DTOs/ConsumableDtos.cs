using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Inventory.DTOs;

public sealed record ConsumableUsagePlanItemDto(
    Guid Id,
    Guid ConsumableUsagePlanId,
    Guid InventoryItemId,
    string ItemName,
    string Sku,
    string UnitOfMeasure,
    decimal CurrentStock,
    decimal WeightedAverageCostBdt,
    decimal PlannedQuantityPerDay,
    string? Notes);

public sealed record ConsumableUsagePlanDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string? Description,
    ConsumableUsagePlanStatus Status,
    string StatusName,
    DateOnly StartDate,
    DateOnly? EndDate,
    int ItemsCount,
    decimal TotalDailyEstimatedCostBdt,
    IReadOnlyList<ConsumableUsagePlanItemDto> Items);

public sealed record DailyConsumableEntryDto(
    Guid Id,
    Guid FarmId,
    Guid ConsumableUsagePlanId,
    string PlanName,
    Guid ConsumableUsagePlanItemId,
    Guid InventoryItemId,
    string ItemName,
    string Sku,
    string UnitOfMeasure,
    decimal CurrentStock,
    DateOnly EntryDate,
    decimal ExpectedQuantity,
    decimal? ActualQuantity,
    decimal? UnitCostAtConsumptionBdt,
    decimal? TotalCostBdt,
    DailyConsumableEntryStatus Status,
    string StatusName,
    string? AdjustmentReason,
    Guid? InventoryTransactionId);

public sealed record DailyConsumableSummaryDto(
    DateOnly Date,
    int TotalPlannedEntries,
    int ConfirmedEntries,
    int SkippedEntries,
    int PendingEntries,
    decimal TotalExpectedQuantity,
    decimal TotalActualQuantity,
    decimal TotalCostBdt);
