using Farm360.Application.Health.DTOs;
using Farm360.Domain.Health;

namespace Farm360.Application.Health.Mappings;

public static class HealthMappingExtensions
{
    public static VaccinationProtocolDto ToDto(this VaccinationProtocol protocol)
    {
        return new VaccinationProtocolDto(
            protocol.Id,
            protocol.Title,
            protocol.TargetSpecies,
            protocol.Description,
            protocol.IsActive,
            protocol.Steps.Select(s => new VaccinationProtocolStepDto(
                s.Id,
                s.StepOrder,
                s.StepName,
                s.TargetAgeDays,
                s.VaccineName,
                s.DosageInstruction
            )).ToList()
        );
    }

    public static VaccinationEventDto ToDto(this VaccinationEvent @event)
    {
        return new VaccinationEventDto(
            @event.Id,
            @event.AnimalId,
            @event.VaccineName,
            @event.BatchNumber,
            @event.ScheduledDate,
            @event.AdministeredDate,
            @event.Status,
            @event.Notes
        );
    }

    public static MedicalTreatmentDto ToDto(this MedicalTreatment treatment)
    {
        return new MedicalTreatmentDto(
            treatment.Id,
            treatment.AnimalId,
            treatment.Diagnosis,
            treatment.MedicationName,
            treatment.Dosage.Amount,
            treatment.Dosage.Unit,
            treatment.WithdrawalPeriod.MilkDays,
            treatment.WithdrawalPeriod.MeatDays,
            treatment.StartDate,
            treatment.EndDate,
            treatment.CostBdt,
            treatment.VeterinarianName,
            treatment.Status,
            treatment.Notes
        );
    }

    public static DiseaseIncidentDto ToDto(this DiseaseIncident incident)
    {
        return new DiseaseIncidentDto(
            incident.Id,
            incident.FarmId,
            incident.ShedId,
            incident.DiseaseName,
            incident.Severity,
            incident.IncidentDate,
            incident.Symptoms,
            incident.AffectedAnimalCount,
            incident.Status,
            incident.Notes
        );
    }
    public static MortalityRecordDto ToDto(this MortalityRecord record)
    {
        return new MortalityRecordDto(
            record.Id,
            record.AnimalId,
            record.DeathDate,
            record.CauseOfDeath,
            record.DiseaseName,
            record.PostMortemNotes,
            record.EstimatedEconomicLossBdt,
            record.DiseaseIncidentId,
            record.RecordedByUserId
        );
    }

    public static VetVisitDto ToDto(this VetVisit visit)
    {
        return new VetVisitDto(
            visit.Id,
            visit.FarmId,
            visit.VetName,
            visit.VisitDate,
            visit.VisitType,
            visit.Purpose,
            visit.Findings,
            visit.Recommendations,
            visit.CostBdt,
            visit.NextVisitDate
        );
    }
}
