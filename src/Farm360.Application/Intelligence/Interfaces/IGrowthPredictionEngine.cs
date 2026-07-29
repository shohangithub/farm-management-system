using Farm360.Domain.Intelligence.ValueObjects;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Interfaces;

public interface IGrowthPredictionEngine
{
    /// <summary>
    /// Calculates the projected growth curve for an animal based on its weight history.
    /// </summary>
    Task<GrowthCurve?> CalculateGrowthCurveAsync(Guid animalId, CancellationToken cancellationToken = default);
}
