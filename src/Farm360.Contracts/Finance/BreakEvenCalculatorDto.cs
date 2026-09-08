using System;

namespace Farm360.Contracts.Finance;

public record BreakEvenCalculatorDto(
    Guid AnimalId,
    Guid FarmId,
    decimal CurrentWeightKg,
    decimal TotalAccumulatedCostBdt,
    decimal BreakEvenPricePerKgBdt,
    string? TagId = null,
    decimal TargetPrice10PercentMarginPerKg = 0m,
    decimal TargetPrice20PercentMarginPerKg = 0m,
    decimal TargetPrice30PercentMarginPerKg = 0m
);
