using System;

namespace Farm360.Contracts.Finance;

public record BreakEvenCalculatorDto(
    Guid AnimalId,
    Guid FarmId,
    decimal CurrentWeightKg,
    decimal TotalAccumulatedCostBdt,
    decimal BreakEvenPricePerKgBdt
);
