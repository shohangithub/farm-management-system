using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Health.Interfaces.Repositories;

public interface IVaccinationRepository
{
    // Protocols
    Task<VaccinationProtocol?> GetProtocolByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationProtocol>> GetProtocolsBySpeciesAsync(AnimalSpecies species, CancellationToken ct = default);
    Task<(IReadOnlyList<VaccinationProtocol> Items, int TotalCount)> GetPagedProtocolsAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);
    void AddProtocol(VaccinationProtocol protocol);
    void UpdateProtocol(VaccinationProtocol protocol);

    // Vaccination Events
    Task<VaccinationEvent?> GetEventByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetEventsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<IReadOnlyList<VaccinationEvent>> GetUpcomingEventsAsync(Guid farmId, DateOnly beforeDate, CancellationToken ct = default);
    Task<(IReadOnlyList<(VaccinationEvent Event, string AnimalTag)> Items, int TotalCount)> GetDewormingEventsAsync(Guid farmId, int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Every vaccination event (any status) for animals still on the farm that was due or given
    /// inside <paramref name="from"/>..<paramref name="to"/>, with the animal's tag and the protocol
    /// title. Read-only; used by the vaccination compliance report.
    /// </summary>
    Task<IReadOnlyList<(VaccinationEvent Event, string AnimalTag, string? ProtocolTitle)>> GetComplianceEventsAsync(
        Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>
    /// Doses actually given (administered date inside the range) to a farm's animals, whatever the
    /// animal's status now — a dose given before a sale was still given. Read-only; for reports.
    /// </summary>
    Task<IReadOnlyList<(VaccinationEvent Event, string AnimalTag)>> GetAdministeredByFarmAsync(
        Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default);
    void AddEvent(VaccinationEvent @event);
    void AddEvents(IEnumerable<VaccinationEvent> events);
    void UpdateEvent(VaccinationEvent @event);
}

public interface IMedicalTreatmentRepository
{
    Task<MedicalTreatment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicalTreatment>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<(IReadOnlyList<MedicalTreatment> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? animalId = null,
        TreatmentStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);
    Task<bool> HasActiveTreatmentForMedicationAsync(Guid animalId, string medicationName, CancellationToken ct = default);
    Task<IReadOnlyList<(MedicalTreatment Treatment, string AnimalTag)>> GetActiveMilkWithdrawalsAsync(Guid farmId, CancellationToken ct = default);

    /// <summary>
    /// Every treatment (any status) of a farm's animals — on the farm or since exited — that started
    /// inside [<paramref name="from"/>, <paramref name="to"/>], oldest first. Read-only; for reports.
    /// </summary>
    Task<IReadOnlyList<MedicalTreatment>> GetByFarmStartedBetweenAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default);
    void Add(MedicalTreatment treatment);
    void Update(MedicalTreatment treatment);
}

public interface IDiseaseIncidentRepository
{
    Task<DiseaseIncident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DiseaseIncident>> GetActiveIncidentsByFarmAsync(Guid farmId, CancellationToken ct = default);
    Task<IReadOnlyList<DiseaseIncident>> GetIncidentsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<(IReadOnlyList<DiseaseIncident> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        IncidentStatus? status = null,
        IncidentSeverity? severity = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);

    /// <summary>Every incident of a farm dated inside [<paramref name="from"/>, <paramref name="to"/>]. Read-only; for reports.</summary>
    Task<IReadOnlyList<DiseaseIncident>> GetByFarmBetweenAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default);
    void Add(DiseaseIncident incident);
    void Update(DiseaseIncident incident);
}

public interface IMortalityRecordRepository
{
    Task<bool> ExistsByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<(IReadOnlyList<MortalityRecord> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? animalId = null,
        string? reason = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);

    /// <summary>Mortality records of a farm's animals with a death date inside [<paramref name="from"/>, <paramref name="to"/>]. Read-only; for reports.</summary>
    Task<IReadOnlyList<MortalityRecord>> GetByFarmBetweenAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default);
    void Add(MortalityRecord record);

    /// <summary>The animal's mortality records, tracked (to remove them when a death is undone).</summary>
    Task<IReadOnlyList<MortalityRecord>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default);

    /// <summary>Removes a record (soft-deleted by the audit interceptor, so it stays in the audit trail).</summary>
    void Remove(MortalityRecord record);
}

public interface IVetVisitRepository
{
    Task<VetVisit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<VetVisit> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default);
    void Add(VetVisit visit);
    void Update(VetVisit visit);
}

public interface IHealthDashboardRepository
{
    Task<int> GetVaccinationsDueThisWeekAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
    Task<int> GetVaccinationsOverdueAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
    Task<int> GetActiveTreatmentsAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
    Task<int> GetActiveIncidentsAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
    Task<int> GetRecentMortalityCountAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
    Task<decimal> GetMonthlyHealthCostAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default);
}
