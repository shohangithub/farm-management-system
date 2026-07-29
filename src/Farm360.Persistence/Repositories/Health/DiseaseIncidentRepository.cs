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

    public async Task<IReadOnlyList<DiseaseIncident>> GetIncidentsByAnimalIdAsync(Guid animalId, CancellationToken ct = default)
    {
        return await context.DiseaseIncidents
            .Where(di => di.AffectedAnimalIds.Contains(animalId))
            .OrderByDescending(di => di.IncidentDate)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<DiseaseIncident> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        IncidentStatus? status = null,
        IncidentSeverity? severity = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = context.DiseaseIncidents.AsNoTracking().AsQueryable();

        if (farmId.HasValue)
        {
            query = query.Where(di => di.FarmId == farmId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(di => di.Status == status.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(di => di.Severity == severity.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(di => EF.Functions.Like(di.DiseaseName, $"%{term}%") ||
                                      (di.Notes != null && EF.Functions.Like(di.Notes, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("incidentdate", false)  => query.OrderBy(di => di.IncidentDate),
            ("incidentdate", true)   => query.OrderByDescending(di => di.IncidentDate),
            ("diseasename", false)   => query.OrderBy(di => di.DiseaseName),
            ("diseasename", true)    => query.OrderByDescending(di => di.DiseaseName),
            ("severity", false)      => query.OrderBy(di => di.Severity),
            ("severity", true)       => query.OrderByDescending(di => di.Severity),
            ("createdat", false)     => query.OrderBy(di => di.CreatedAtUtc),
            _                        => query.OrderByDescending(di => di.IncidentDate)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(DiseaseIncident incident) => context.DiseaseIncidents.Add(incident);
    public void Update(DiseaseIncident incident) => context.DiseaseIncidents.Update(incident);
}
