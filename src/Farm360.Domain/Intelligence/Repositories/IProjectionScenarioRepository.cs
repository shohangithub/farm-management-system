using Farm360.Domain.Intelligence.Projections;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Intelligence.Repositories;

public interface IProjectionScenarioRepository
{
    Task<ProjectionScenario?> GetByIdAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectionScenario>> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default);
    void Add(ProjectionScenario scenario);
}
