using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Interfaces;

public interface ISimulationEngine
{
    Task<SaleSimulationResult?> SimulateSaleAsync(Guid animalId, DateOnly targetDate, CancellationToken cancellationToken = default);
}

public sealed record SaleSimulationResult(
    Guid AnimalId,
    DateOnly TargetDate,
    int DaysFromNow,
    decimal ProjectedWeightKg,
    decimal EstimatedSalePriceBdt,
    decimal ProjectedAdditionalCostBdt,
    decimal ProjectedTotalCostBdt,
    decimal ProjectedProfitMarginBdt
);
