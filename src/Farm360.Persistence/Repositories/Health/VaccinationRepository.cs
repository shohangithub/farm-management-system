using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class VaccinationRepository(ApplicationDbContext context) : IVaccinationRepository
{
    public async Task<VaccinationProtocol?> GetProtocolByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.VaccinationProtocols
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<VaccinationProtocol>> GetProtocolsBySpeciesAsync(AnimalSpecies species, CancellationToken ct = default)
    {
        return await context.VaccinationProtocols
            .Include(p => p.Steps)
            .Where(p => p.TargetSpecies == species && p.IsActive)
            .ToListAsync(ct);
    }

    public void AddProtocol(VaccinationProtocol protocol) => context.VaccinationProtocols.Add(protocol);
    public void UpdateProtocol(VaccinationProtocol protocol) => context.VaccinationProtocols.Update(protocol);

    public async Task<VaccinationEvent?> GetEventByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.VaccinationEvents
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<VaccinationEvent>> GetEventsByAnimalIdAsync(Guid animalId, CancellationToken ct = default)
    {
        return await context.VaccinationEvents
            .Where(e => e.AnimalId == animalId)
            .OrderByDescending(e => e.ScheduledDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VaccinationEvent>> GetUpcomingEventsAsync(Guid farmId, DateOnly beforeDate, CancellationToken ct = default)
    {
        return await context.VaccinationEvents
            .Join(context.Animals,
                ve => ve.AnimalId,
                a => a.Id,
                (ve, a) => new { Event = ve, Animal = a })
            .Where(x => x.Animal.FarmId == farmId && 
                        x.Event.ScheduledDate <= beforeDate && 
                        x.Event.Status == Domain.Health.Enums.VaccinationStatus.Scheduled)
            .Select(x => x.Event)
            .OrderBy(e => e.ScheduledDate)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<VaccinationEvent> Items, int TotalCount)> GetDewormingEventsAsync(Guid farmId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var dewormingStepIds = context.VaccinationProtocols
            .Where(p => p.IsDeworming)
            .SelectMany(p => p.Steps)
            .Select(s => s.Id);

        var animalIdsInFarm = context.Animals
            .Where(a => a.FarmId == farmId)
            .Select(a => a.Id);

        var query = context.VaccinationEvents
            .Where(ve => animalIdsInFarm.Contains(ve.AnimalId) && 
                         ve.ProtocolStepId != null && 
                         dewormingStepIds.Contains(ve.ProtocolStepId.Value));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.ScheduledDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<VaccinationProtocol> Items, int TotalCount)> GetPagedProtocolsAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = context.VaccinationProtocols
            .Include(p => p.Steps)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p => EF.Functions.Like(p.Title, $"%{term}%") || (p.Description != null && EF.Functions.Like(p.Description, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("title", false)       => query.OrderBy(p => p.Title),
            ("title", true)        => query.OrderByDescending(p => p.Title),
            ("targetspecies", false) => query.OrderBy(p => p.TargetSpecies),
            ("targetspecies", true)  => query.OrderByDescending(p => p.TargetSpecies),
            ("createdat", false)   => query.OrderBy(p => p.CreatedAtUtc),
            _                      => query.OrderByDescending(p => p.CreatedAtUtc)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void AddEvent(VaccinationEvent @event) => context.VaccinationEvents.Add(@event);
    public void AddEvents(IEnumerable<VaccinationEvent> events) => context.VaccinationEvents.AddRange(events);
    public void UpdateEvent(VaccinationEvent @event) => context.VaccinationEvents.Update(@event);
}
