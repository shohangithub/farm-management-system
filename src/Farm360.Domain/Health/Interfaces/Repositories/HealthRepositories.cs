using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Health.Interfaces.Repositories;

public interface IVaccinationRepository
{
    // Protocols
    Task<VaccinationProtocol?> GetProtocolByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationProtocol>> GetProtocolsBySpeciesAsync(AnimalSpecies species, CancellationToken ct = default);
    void AddProtocol(VaccinationProtocol protocol);
    void UpdateProtocol(VaccinationProtocol protocol);

    // Vaccination Events
    Task<VaccinationEvent?> GetEventByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetEventsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetUpcomingEventsAsync(Guid farmId, DateOnly beforeDate, CancellationToken ct = default);
    void AddEvent(VaccinationEvent @event);
    void UpdateEvent(VaccinationEvent @event);
}

public interface IMedicalTreatmentRepository
{
    Task<MedicalTreatment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicalTreatment>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<bool> HasActiveTreatmentForMedicationAsync(Guid animalId, string medicationName, CancellationToken ct = default);
    void Add(MedicalTreatment treatment);
    void Update(MedicalTreatment treatment);
}

public interface IDiseaseIncidentRepository
{
    Task<DiseaseIncident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiseaseIncident>> GetActiveIncidentsByFarmAsync(Guid farmId, CancellationToken ct = default);
    void Add(DiseaseIncident incident);
    void Update(DiseaseIncident incident);
}
