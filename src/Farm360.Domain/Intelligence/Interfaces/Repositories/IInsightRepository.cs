using Farm360.Domain.Interfaces.Repositories;
using Farm360.Domain.Intelligence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Intelligence.Interfaces.Repositories;

public interface IInsightRepository
{
    Task<ActionableInsight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ActionableInsight insight, CancellationToken cancellationToken = default);
    Task<List<ActionableInsight>> GetActiveInsightsByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default);
    Task<List<ActionableInsight>> GetActiveInsightsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
}
