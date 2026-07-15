using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class DiseaseIncidentRepository(ApplicationDbContext context) : IDiseaseIncidentRepository
{
    public async Task<DiseaseIncident?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.DiseaseIncidents
            .FirstOrDefaultAsync(di => di.Id == id, ct);
    }

    public async Task<IReadOnlyList<DiseaseIncident>> GetActiveIncidentsByFarmAsync(Guid farmId, CancellationToken ct = default)
    {
        return await context.DiseaseIncidents
            .Where(di => di.FarmId == farmId && 
                         (di.Status == IncidentStatus.Reported || di.Status == IncidentStatus.UnderTreatment))
            .OrderByDescending(di => di.IncidentDate)
            .ToListAsync(ct);
    }

    public void Add(DiseaseIncident incident) => context.DiseaseIncidents.Add(incident);
    public void Update(DiseaseIncident incident) => context.DiseaseIncidents.Update(incident);
}
