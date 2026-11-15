using System;

namespace Farm360.Contracts.Finance;

/// <param name="TotalAccumulatedCostBdt">The animal's cost-ledger total (acquisition + feed + vet + labour + overhead).</param>
/// <param name="BreakEvenPricePerKgBdt">Sale price per kg of LIVE weight that recovers the total cost.</param>
/// <param name="TargetPrice10PercentMarginPerKg">Price per live kg for a 10 % margin on cost (likewise 20 % / 30 %).</param>
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
