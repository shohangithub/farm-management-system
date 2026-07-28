using Farm360.Domain.Feeding.Enums;

namespace Farm360.Application.Feeding.DTOs;

public record FeedIngredientDto(
    Guid Id,
    Guid TenantId,
    string Name,
    FeedCategory Category,
    string CategoryName,
    decimal DryMatterPct,
    decimal CrudeProteinPct,
    decimal MetabolizableEnergyMjPerKg,
    decimal CrudeFiberPct,
    decimal CalciumPct,
    decimal PhosphorusPct,
    string Unit,
    decimal UnitCostBdt,
    bool IsPreloaded,
    bool IsActive,
    string? Description);

public record FormulaIngredientDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Percentage,
    decimal IngredientCostPerKg,
    decimal DryMatterPct,
    decimal CrudeProteinPct,
    decimal MetabolizableEnergyMjPerKg);

public record FeedFormulaDto(
    Guid Id,
    Guid TenantId,
    string Title,
    TargetAnimalType TargetSpecies,
    string TargetSpeciesName,
    string? TargetStage,
    FormulaStatus Status,
    string StatusName,
    string? Description,
    decimal TotalCostPerKgBdt,
    decimal DryMatterPct,
    decimal CrudeProteinPct,
    decimal MetabolizableEnergyMjPerKg,
    IReadOnlyList<FormulaIngredientDto> Ingredients);

public record FeedingScheduleDto(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    Guid? ShedId,
    string? ShedNumber,
    Guid? PenId,
    string? PenNumber,
    Guid? BatchId,
    string? BatchName,
    Guid FormulaId,
    string FormulaTitle,
    string Title,
    decimal TargetQuantityKgPerHead,
    ScheduleFrequency Frequency,
    string FrequencyName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    string? Notes);

public record ConsumptionDetailDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal OfferedKg,
    decimal RefusalKg,
    decimal NetConsumedKg,
    decimal CostBdt);

public record FeedConsumptionLogDto(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    Guid? ShedId,
    string? ShedNumber,
    Guid? PenId,
    string? PenNumber,
    Guid? BatchId,
    string? BatchName,
    Guid FormulaId,
    string FormulaTitle,
    DateOnly LogDate,
    int HeadCount,
    decimal TotalFeedOfferedKg,
    decimal TotalRefusalKg,
    decimal NetConsumptionKg,
    decimal TotalCostBdt,
    string? LoggedByUserId,
    string? Notes,
    IReadOnlyList<ConsumptionDetailDto> Details);

public record FcrAnalyticsDto(
    Guid FarmId,
    Guid? ShedId,
    string? ShedNumber,
    decimal TotalFeedConsumedKg,
    decimal TotalWeightGainKg,
    decimal FcrValue,
    decimal TotalFeedCostBdt,
    decimal CostPerKgGainBdt,
    IReadOnlyList<MonthlyFcrDataPointDto> MonthlyTrends);

public record MonthlyFcrDataPointDto(
    string Month,
    decimal FeedConsumedKg,
    decimal WeightGainKg,
    decimal FcrValue);
