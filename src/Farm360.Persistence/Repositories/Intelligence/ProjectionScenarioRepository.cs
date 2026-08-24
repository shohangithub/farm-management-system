using Farm360.Domain.Intelligence.Projections;
using Farm360.Domain.Intelligence.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Intelligence;

#pragma warning disable CA1812
internal sealed class ProjectionScenarioRepository : IProjectionScenarioRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectionScenarioRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectionScenario?> GetByIdAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectionScenarios
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectionScenario>> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectionScenarios
            .Where(s => s.AnimalId == animalId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Add(ProjectionScenario scenario)
    {
        _dbContext.ProjectionScenarios.Add(scenario);
    }
}
