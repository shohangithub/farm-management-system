using Farm360.Domain.Exceptions;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Health;

/// <summary>Recurring steps, care tasks, skipping/rescheduling and weight-based doses.</summary>
public sealed class HealthScheduleTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid AnimalId = Guid.NewGuid();

    private static VaccinationEvent FromStep(ProtocolStepOptions options, DateOnly? date = null, Guid? itemId = null, decimal? qty = null)
    {
        var protocol = VaccinationProtocol.Create(Tenant, "Programme", AnimalSpecies.CattleBeef, null);
        var step = protocol.AddStep("Dewormer", 93, "Dewormer", "empty stomach", itemId, qty, options);
        return VaccinationEvent.ScheduleFromStep(Tenant, AnimalId, protocol.Id, step, date ?? Start, null);
    }

    // ── Weight-based dose ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(DoseRounding.Half, 290, 2.0)]   // 1.93 → up to the next half
    [InlineData(DoseRounding.Half, 160, 1.5)]   // 1.07 → 1.5
    [InlineData(DoseRounding.Whole, 160, 2.0)]  // 1.07 → 2
    [InlineData(DoseRounding.None, 290, 1.93)]  // 2 decimals
    [InlineData(DoseRounding.Whole, 300, 2.0)]  // exact stays
    public void DoseFor_RoundsUp(DoseRounding rounding, double weightKg, double expected)
    {
        var rule = new WeightDoseRule(1m, 150m, rounding, unit: "tablet");
        rule.DoseFor((decimal)weightKg).Should().Be((decimal)expected);
    }

    [Fact]
    public void DoseFor_KeepsWithinMinAndMax()
    {
        var rule = new WeightDoseRule(1m, 50m, DoseRounding.Whole, min: 2m, max: 6m, unit: "ml");
        rule.DoseFor(40m).Should().Be(2m);   // 0.8 → 1, raised to the minimum
        rule.DoseFor(220m).Should().Be(5m);  // 4.4 → 5
        rule.DoseFor(600m).Should().Be(6m);  // 12, capped at the maximum
    }

    [Fact]
    public void DoseRule_RejectsMinAboveMax_AndZeroWeight()
    {
        FluentActions.Invoking(() => new WeightDoseRule(1m, 150m, min: 5m, max: 2m)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => new WeightDoseRule(1m, 0m)).Should().Throw<DomainException>();
        new WeightDoseRule(1m, 150m).Invoking(r => r.DoseFor(0m)).Should().Throw<DomainException>();
    }

    [Fact]
    public void ApplyWeight_SetsDoseAndFlagsEstimatedWeight()
    {
        var task = FromStep(new ProtocolStepOptions(DoseRule: new WeightDoseRule(1m, 150m, DoseRounding.Half, unit: "tablet")));

        task.ApplyWeight(150m, estimated: true).Should().Be(1m);
        task.DoseWeightEstimated.Should().BeTrue();

        task.ApplyWeight(290m, estimated: false).Should().Be(2m);
        task.DosageQuantity.Should().Be(2m);
        task.DoseWeightKg.Should().Be(290m);
        task.DoseWeightEstimated.Should().BeFalse();
        task.DoseUnit.Should().Be("tablet");
    }

    // ── Recurring steps ────────────────────────────────────────────────────────

    [Fact]
    public void NextOccurrence_CountsFromTheActualDate_WhenGivenLate()
    {
        var item = Guid.NewGuid();
        var task = FromStep(new ProtocolStepOptions(RepeatEveryDays: 90), itemId: item, qty: 10m);

        var givenLate = Start.AddDays(6);
        task.RecordAdministration(givenLate, Guid.NewGuid(), today: givenLate);
        var next = task.CreateNextOccurrence();

        next.Should().NotBeNull();
        next!.ScheduledDate.Should().Be(givenLate.AddDays(90));
        next.PreviousEventId.Should().Be(task.Id);
        next.Status.Should().Be(VaccinationStatus.Scheduled);
        next.RepeatEveryDays.Should().Be(90);
        next.InventoryItemId.Should().Be(item);
        next.DosageQuantity.Should().Be(10m);
        next.ProtocolStepId.Should().Be(task.ProtocolStepId);
    }

    [Fact]
    public void NextOccurrence_AfterSkip_CountsFromTheSkipDate()
    {
        var task = FromStep(new ProtocolStepOptions(RepeatEveryDays: 30));
        task.Skip(Start.AddDays(2), "Animal sick, vet said wait", Start.AddDays(2));

        task.Status.Should().Be(VaccinationStatus.Skipped);
        task.SkipReason.Should().Be("Animal sick, vet said wait");
        task.CreateNextOccurrence()!.ScheduledDate.Should().Be(Start.AddDays(32));
    }

    [Fact]
    public void NextOccurrence_IsNull_ForOneOffOrPendingTasks()
    {
        var oneOff = FromStep(new ProtocolStepOptions());
        oneOff.RecordAdministration(Start, Guid.NewGuid(), today: Start);
        oneOff.CreateNextOccurrence().Should().BeNull();

        FromStep(new ProtocolStepOptions(RepeatEveryDays: 90)).CreateNextOccurrence().Should().BeNull();
    }

    [Fact]
    public void NextOccurrence_NeverLandsBeforeTheFloor()
    {
        var task = FromStep(new ProtocolStepOptions(RepeatEveryDays: 10));
        task.RecordAdministration(Start, Guid.NewGuid(), today: Start);
        task.CreateNextOccurrence(notBefore: Start.AddDays(40))!.ScheduledDate.Should().Be(Start.AddDays(40));
    }

    [Fact]
    public void AddStep_RejectsRepeatBelowOneDay()
    {
        var protocol = VaccinationProtocol.Create(Tenant, "P", AnimalSpecies.CattleBeef, null);
        protocol.Invoking(p => p.AddStep("x", 0, "x", "", options: new ProtocolStepOptions(RepeatEveryDays: 0)))
            .Should().Throw<DomainException>();
    }

    // ── Care tasks, skip and reschedule ────────────────────────────────────────

    [Fact]
    public void CareTask_DropsStockLink_AndDeductsNothingWhenDone()
    {
        var protocol = VaccinationProtocol.Create(Tenant, "P", AnimalSpecies.CattleBeef, null);
        var step = protocol.AddStep("Potash wash", 0, "Potash wash", "", Guid.NewGuid(), 5m, new ProtocolStepOptions(HealthTaskKind.CareTask));
        step.InventoryItemId.Should().BeNull();
        step.DosageQuantity.Should().BeNull();

        var task = VaccinationEvent.ScheduleFromStep(Tenant, AnimalId, protocol.Id, step, Start, null);
        task.RecordAdministration(Start, Guid.NewGuid(), today: Start);

        task.IsCareTask.Should().BeTrue();
        var administered = task.DomainEvents.OfType<Farm360.Domain.Health.Events.VaccinationAdministeredEvent>().Single();
        administered.InventoryItemId.Should().BeNull();
        administered.DosageQuantity.Should().BeNull();
    }

    [Fact]
    public void RecordAdministration_UsesTheConvertedStockQuantity()
    {
        var task = FromStep(new ProtocolStepOptions(DoseRule: new WeightDoseRule(1m, 100m, unit: "ml")), itemId: Guid.NewGuid());
        task.ApplyWeight(250m, false);
        task.RecordAdministration(Start, Guid.NewGuid(), today: Start, stockQuantity: 0.0025m);

        task.DomainEvents.OfType<Farm360.Domain.Health.Events.VaccinationAdministeredEvent>().Single()
            .DosageQuantity.Should().Be(0.0025m);
    }

    [Fact]
    public void Skip_NeedsAReason_AndOnlyForPendingTasks()
    {
        var task = FromStep(new ProtocolStepOptions());
        task.Invoking(t => t.Skip(Start, " ", Start)).Should().Throw<DomainException>();
        task.Invoking(t => t.Skip(Start.AddDays(1), "later", Start)).Should().Throw<DomainException>();

        task.RecordAdministration(Start, Guid.NewGuid(), today: Start);
        task.Invoking(t => t.Skip(Start, "reason", Start)).Should().Throw<DomainException>();
    }

    [Fact]
    public void SkippedTask_CannotBeGivenAfterwards()
    {
        var task = FromStep(new ProtocolStepOptions());
        task.Skip(Start, "not needed", Start);
        task.Invoking(t => t.RecordAdministration(Start, Guid.NewGuid(), today: Start)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Reschedule_MovesAPendingTask_NotIntoThePast()
    {
        var task = FromStep(new ProtocolStepOptions());
        task.MarkOverdue(Start.AddDays(3));
        task.Status.Should().Be(VaccinationStatus.Overdue);

        task.Reschedule(Start.AddDays(5), Start.AddDays(3));
        task.ScheduledDate.Should().Be(Start.AddDays(5));
        task.Status.Should().Be(VaccinationStatus.Scheduled);

        task.Invoking(t => t.Reschedule(Start, Start.AddDays(3))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Protocol_SpanDays_CountsTheStartDay()
    {
        var protocol = VaccinationProtocol.Create(Tenant, "P", AnimalSpecies.CattleBeef, null);
        protocol.AddStep("a", 0, "a", "");
        protocol.AddStep("b", 107, "b", "");
        protocol.SpanDays.Should().Be(108);
    }
}
