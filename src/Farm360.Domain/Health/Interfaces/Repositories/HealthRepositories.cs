using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Health.Interfaces.Repositories;

public interface IVaccinationRepository
{
    // Protocols
    Task<VaccinationProtocol?> GetProtocolByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationProtocol>> GetProtocolsBySpeciesAsync(AnimalSpecies species, CancellationToken ct = default);
    Task<(IReadOnlyList<VaccinationProtocol> Items, int TotalCount)> GetPagedProtocolsAsync(int pageNumber, int pageSize, string? searchTerm, CancellationToken ct = default);
    void AddProtocol(VaccinationProtocol protocol);
    void UpdateProtocol(VaccinationProtocol protocol);

    // Vaccination Events
    Task<VaccinationEvent?> GetEventByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetEventsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetUpcomingEventsAsync(Guid farmId, DateOnly beforeDate, CancellationToken ct = default);
    Task<(IReadOnlyList<VaccinationEvent> Items, int TotalCount)> GetDewormingEventsAsync(Guid farmId, int pageNumber, int pageSize, CancellationToken ct = default);
    void AddEvent(VaccinationEvent @event);
    void AddEvents(IEnumerable<VaccinationEvent> events);
    void UpdateEvent(VaccinationEvent @event);
}

public interface IMedicalTreatmentRepository
{
    Task<MedicalTreatment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicalTreatment>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<(IReadOnlyList<MedicalTreatment> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? animalId, CancellationToken ct = default);
    Task<bool> HasActiveTreatmentForMedicationAsync(Guid animalId, string medicationName, CancellationToken ct = default);
    Task<IReadOnlyList<(MedicalTreatment Treatment, string AnimalTag)>> GetActiveMilkWithdrawalsAsync(Guid farmId, CancellationToken ct = default);
    void Add(MedicalTreatment treatment);
    void Update(MedicalTreatment treatment);
}

public interface IDiseaseIncidentRepository
{
    Task<DiseaseIncident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiseaseIncident>> GetActiveIncidentsByFarmAsync(Guid farmId, CancellationToken ct = default);
    Task<IReadOnlyList<DiseaseIncident>> GetIncidentsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<(IReadOnlyList<DiseaseIncident> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    void Add(DiseaseIncident incident);
    void Update(DiseaseIncident incident);
}

public interface IMortalityRecordRepository
{
    Task<(IReadOnlyList<MortalityRecord> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    void Add(MortalityRecord record);
}

public interface IVetVisitRepository
{
    Task<VetVisit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<VetVisit> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? farmId, CancellationToken ct = default);
    void Add(VetVisit visit);
    void Update(VetVisit visit);
}

public interface IHealthDashboardRepository
{
    Task<int> GetVaccinationsDueThisWeekAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetVaccinationsOverdueAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetActiveTreatmentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetActiveIncidentsAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetRecentMortalityCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<decimal> GetMonthlyHealthCostAsync(Guid tenantId, CancellationToken ct = default);
}
