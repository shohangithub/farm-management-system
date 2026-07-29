using Farm360.Domain.Intelligence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Interfaces;

public interface IRuleEngine
{
    /// <summary>
    /// Evaluates the performance of an animal and generates actionable insights.
    /// </summary>
    Task<List<ActionableInsight>> EvaluateAnimalPerformanceAsync(Guid animalId, CancellationToken cancellationToken = default);
}
