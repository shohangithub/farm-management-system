using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Exceptions;
using Farm360.Domain.Health.ValueObjects;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Health;

public sealed class HealthDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AnimalId = Guid.NewGuid();
    private static readonly Guid FarmId   = Guid.NewGuid();

    // ══════════════════════════════════════════════════════════════════════════
    // VaccinationProtocol Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VaccinationProtocol_Create_ValidData_ReturnsActiveProtocol()
    {
        var protocol = VaccinationProtocol.Create(TenantId, "FMD Standard Protocol", AnimalSpecies.CattleBeef, "Routine 6-month protocol");

        protocol.Title.Should().Be("FMD Standard Protocol");
        protocol.TargetSpecies.Should().Be(AnimalSpecies.CattleBeef);
        protocol.IsActive.Should().BeTrue();
        protocol.Steps.Should().BeEmpty();
    }

    [Fact]
    public void VaccinationProtocol_AddStep_IncrementsOrderAndAddsStep()
    {
        var protocol = VaccinationProtocol.Create(TenantId, "FMD Protocol", AnimalSpecies.CattleBeef, null);

        var step1 = protocol.AddStep("Primary Dose", 60, "FMD Vaccine", "2 ml SubQ");
        var step2 = protocol.AddStep("Booster Dose", 81, "FMD Vaccine", "2 ml SubQ");

        protocol.Steps.Should().HaveCount(2);
        step1.StepOrder.Should().Be(1);
        step2.StepOrder.Should().Be(2);
        step2.VaccineName.Should().Be("FMD Vaccine");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VaccinationEvent Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VaccinationEvent_Schedule_CreatesScheduledEventWithDomainEvent()
    {
        var @event = VaccinationEvent.Schedule(
            TenantId, AnimalId, null, "Anthrax Vaccine", "B-2026-01", new DateOnly(2026, 8, 1), "Annual booster");

        @event.Status.Should().Be(VaccinationStatus.Scheduled);
        @event.VaccineName.Should().Be("Anthrax Vaccine");
        @event.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "VaccinationScheduledEvent");
    }

    [Fact]
    public void VaccinationEvent_RecordAdministration_CompletesAndRaisesEvent()
    {
        var @event = VaccinationEvent.Schedule(
            TenantId, AnimalId, null, "Anthrax Vaccine", "B-2026-01", new DateOnly(2026, 8, 1), null);

        var adminBy = Guid.NewGuid();
        var adminDate = DateOnly.FromDateTime(DateTime.UtcNow);

        @event.RecordAdministration(adminDate, adminBy, "Administered successfully");

        @event.Status.Should().Be(VaccinationStatus.Completed);
        @event.AdministeredDate.Should().Be(adminDate);
        @event.AdministeredBy.Should().Be(adminBy);
        @event.DomainEvents.Should().Contain(e => e.GetType().Name == "VaccinationAdministeredEvent");
    }

    [Fact]
    public void VaccinationEvent_RecordAdministration_FutureDate_ThrowsException()
    {
        var @event = VaccinationEvent.Schedule(
            TenantId, AnimalId, null, "Anthrax Vaccine", "B-2026-01", new DateOnly(2026, 8, 1), null);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var act = () => @event.RecordAdministration(futureDate, Guid.NewGuid());

        act.Should().Throw<FutureVaccinationDateException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MedicalTreatment Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MedicalTreatment_LogTreatment_CreatesOngoingTreatment()
    {
        var dosage = Dosage.Create(5m, "ml");
        var withdrawal = WithdrawalPeriod.Create(7, 28);
        var startDate = new DateOnly(2026, 7, 10);

        var treatment = MedicalTreatment.LogTreatment(
            TenantId, AnimalId, "Mastitis", "Penicillin-G", dosage, withdrawal, startDate, null, 1500m, "Dr. Rahman", "Intramuscular injection");

        treatment.Status.Should().Be(TreatmentStatus.Ongoing);
        treatment.Diagnosis.Should().Be("Mastitis");
        treatment.CostBdt.Should().Be(1500m);
        treatment.WithdrawalPeriod.MilkDays.Should().Be(7);
        treatment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "TreatmentLoggedEvent");
    }

    [Fact]
    public void MedicalTreatment_CompleteTreatment_UpdatesStatusAndEndDate()
    {
        var dosage = Dosage.Create(5m, "ml");
        var treatment = MedicalTreatment.LogTreatment(
            TenantId, AnimalId, "Mastitis", "Penicillin-G", dosage, WithdrawalPeriod.None, new DateOnly(2026, 7, 10), null, 1500m, null, null);

        var endDate = new DateOnly(2026, 7, 15);
        treatment.CompleteTreatment(endDate, "Full recovery observed");

        treatment.Status.Should().Be(TreatmentStatus.Completed);
        treatment.EndDate.Should().Be(endDate);
        treatment.Notes.Should().Be("Full recovery observed");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DiseaseIncident Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DiseaseIncident_Report_CreatesIncidentWithDomainEvent()
    {
        var incident = DiseaseIncident.Report(
            TenantId, FarmId, null, "Lumpy Skin Disease", IncidentSeverity.Severe, new DateOnly(2026, 7, 1), "Skin nodules, high fever", 4, "Isolated affected pen");

        incident.Status.Should().Be(IncidentStatus.Reported);
        incident.DiseaseName.Should().Be("Lumpy Skin Disease");
        incident.AffectedAnimalCount.Should().Be(4);
        incident.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "DiseaseIncidentReportedEvent");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Value Objects Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dosage_InvalidAmount_ThrowsArgumentException()
    {
        var act = () => Dosage.Create(0m, "ml");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithdrawalPeriod_NegativeDays_ThrowsArgumentException()
    {
        var act = () => WithdrawalPeriod.Create(-1, 5);
        act.Should().Throw<ArgumentException>();
    }
}
