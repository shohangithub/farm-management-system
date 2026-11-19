using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.ValueObjects;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Livestock;

/// <summary>
/// Sold/dead animals: pen/shed movement closed at the exit date, and new facts refused on/after it
/// (farm rules in AnimalStatusGroups).
/// </summary>
public sealed class AnimalExitTests
{
    private static readonly DateOnly Acquired = new(2026, 1, 1);
    private static readonly DateOnly Placed = new(2026, 2, 1);
    private static readonly DateOnly SaleDate = new(2026, 11, 13);

    private static Animal NewAnimal(AnimalSex sex = AnimalSex.Female) =>
        Animal.Create(Guid.NewGuid(), Guid.NewGuid(), AnimalTag.Create("C-12", TagType.EarTag),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), sex, new DateOnly(2025, 1, 1),
            AcquisitionType.Purchased, Acquired, 50_000m, null);

    private static Animal PlacedAnimal(AnimalSex sex = AnimalSex.Female)
    {
        var animal = NewAnimal(sex);
        animal.TransferToShed(Guid.NewGuid(), Guid.NewGuid(), Placed, Guid.NewGuid());
        return animal;
    }

    private static Animal SoldAnimal(AnimalSex sex = AnimalSex.Female)
    {
        var animal = PlacedAnimal(sex);
        animal.Sell(90_000m, SaleDate, Guid.NewGuid(), "Karim", 320m);
        return animal;
    }

    // ── Movement closed on exit ────────────────────────────────────────────────

    [Fact]
    public void Sell_ClosesCurrentMovementAtSaleDate_AndKeepsBatch()
    {
        var animal = PlacedAnimal();
        var batchId = Guid.NewGuid();
        animal.AssignToBatch(batchId);
        var movement = animal.CurrentMovement!;

        animal.Sell(90_000m, SaleDate, Guid.NewGuid(), null, null);

        animal.CurrentMovement.Should().BeNull();
        movement.RemovedAtUtc.Should().Be(SaleDate.ToDateTime(TimeOnly.MinValue));
        animal.BatchId.Should().Be(batchId);
        animal.ExitDate.Should().Be(SaleDate);
        animal.HasLeftFarm.Should().BeTrue();
    }

    [Fact]
    public void RecordDeath_ClosesCurrentMovementAtDeathDate()
    {
        var animal = PlacedAnimal();
        var movement = animal.CurrentMovement!;
        var deathDate = new DateOnly(2026, 10, 5);

        animal.RecordDeath(DisposalReason.Disease, deathDate, "FMD");

        animal.CurrentMovement.Should().BeNull();
        movement.RemovedAtUtc.Should().Be(deathDate.ToDateTime(TimeOnly.MinValue));
        animal.ExitDate.Should().Be(deathDate);
    }

    [Fact]
    public void Sell_WithoutMovement_Succeeds()
    {
        var animal = NewAnimal();

        animal.Sell(90_000m, SaleDate, Guid.NewGuid(), null, null);

        animal.Status.Should().Be(AnimalStatus.Sold);
    }

    [Fact]
    public void Sell_DatedBeforeCurrentPlacement_Throws()
    {
        var animal = PlacedAnimal();

        var act = () => animal.Sell(90_000m, Placed.AddDays(-1), Guid.NewGuid(), null, null);

        act.Should().Throw<InvalidExitDateException>();
    }

    [Fact]
    public void RecordDeath_BeforeAcquisition_Throws()
    {
        var animal = NewAnimal();

        var act = () => animal.RecordDeath(DisposalReason.Accident, Acquired.AddDays(-1), null);

        act.Should().Throw<InvalidExitDateException>();
    }

    // ── On farm on day D ───────────────────────────────────────────────────────

    [Fact]
    public void IsOnFarmOn_FollowsAcquisitionAndExitDates()
    {
        var animal = SoldAnimal();

        animal.IsOnFarmOn(Acquired.AddDays(-1)).Should().BeFalse();
        animal.IsOnFarmOn(Acquired).Should().BeTrue();
        animal.IsOnFarmOn(SaleDate.AddDays(-1)).Should().BeTrue();
        animal.IsOnFarmOn(SaleDate).Should().BeFalse();
    }

    [Theory]
    [InlineData(AnimalStatus.Active, false)]
    [InlineData(AnimalStatus.Quarantined, false)]
    [InlineData(AnimalStatus.Sold, true)]
    [InlineData(AnimalStatus.Dead, true)]
    [InlineData(AnimalStatus.Slaughtered, true)]
    [InlineData(AnimalStatus.Transferred, true)]
    public void HasLeftFarm_MatchesStatusGroups(AnimalStatus status, bool left)
    {
        AnimalStatusGroups.HasLeftFarm(status).Should().Be(left);
        AnimalStatusGroups.LeftFarm.Contains(status).Should().Be(left);
    }

    // ── Guards ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RecordWeight_DatedBeforeSale_IsAllowedAsLateEntry()
    {
        var animal = SoldAnimal();

        var record = animal.RecordWeight(Weight.Create(310m), SaleDate.AddDays(-2), Guid.NewGuid(), null);

        record.RecordedDate.Should().Be(SaleDate.AddDays(-2));
    }

    [Fact]
    public void RecordWeight_OnSaleDate_ThrowsNamingSaleDate()
    {
        var animal = SoldAnimal();

        var act = () => animal.RecordWeight(Weight.Create(310m), SaleDate, Guid.NewGuid(), null);

        act.Should().Throw<AnimalHasLeftFarmException>().WithMessage("*sold on 13 Nov 2026*");
    }

    [Fact]
    public void RecordBcs_AfterDeath_ThrowsNamingDeathDate()
    {
        var animal = PlacedAnimal();
        animal.RecordDeath(DisposalReason.Disease, new DateOnly(2026, 10, 5), null);

        var act = () => animal.RecordBodyConditionScore(3m, new DateOnly(2026, 10, 6), Guid.NewGuid(), null);

        act.Should().Throw<AnimalHasLeftFarmException>().WithMessage("*died on 05 Oct 2026*");
    }

    [Fact]
    public void AddBreedingRecord_AfterSale_Throws()
    {
        var animal = SoldAnimal();

        var act = () => animal.AddBreedingRecord(SaleDate.AddDays(1), null, "EXT-1", true, Guid.NewGuid());

        act.Should().Throw<AnimalHasLeftFarmException>();
    }

    [Fact]
    public void ConfirmPregnancy_AfterSale_Throws_ButBeforeSaleIsAllowed()
    {
        var animal = PlacedAnimal();
        var mating = animal.AddBreedingRecord(new DateOnly(2026, 6, 1), null, "EXT-1", true, Guid.NewGuid());
        animal.Sell(90_000m, SaleDate, Guid.NewGuid(), null, null);

        var after = () => animal.ConfirmPregnancy(mating.Id, SaleDate.AddDays(3), new DateOnly(2027, 3, 1));
        after.Should().Throw<AnimalHasLeftFarmException>();

        animal.ConfirmPregnancy(mating.Id, new DateOnly(2026, 8, 1), new DateOnly(2027, 3, 1));
        mating.IsPregnancyConfirmed.Should().BeTrue();
    }

    [Fact]
    public void TransferToShed_SoldAnimal_Throws()
    {
        var animal = SoldAnimal();

        var act = () => animal.TransferToShed(Guid.NewGuid(), null, SaleDate.AddDays(-5), Guid.NewGuid());

        act.Should().Throw<AnimalHasLeftFarmException>();
    }

    [Fact]
    public void AssignToBatch_DeadAnimal_Throws()
    {
        var animal = NewAnimal();
        animal.RecordDeath(DisposalReason.NaturalDeath, new DateOnly(2026, 10, 5), null);

        var act = () => animal.AssignToBatch(Guid.NewGuid());

        act.Should().Throw<AnimalHasLeftFarmException>();
    }

    [Fact]
    public void AddPhoto_SoldAnimal_IsStillAllowed()
    {
        var animal = SoldAnimal();

        animal.AddPhoto("https://example.test/p.jpg", null, Guid.NewGuid());

        animal.Photos.Should().HaveCount(1);
    }

    // ── Health items closed on exit ────────────────────────────────────────────

    [Fact]
    public void VaccinationEvent_CancelForAnimalExit_CancelsPendingOnly()
    {
        var pending = VaccinationEvent.Schedule(Guid.NewGuid(), Guid.NewGuid(), null, "FMD", "", SaleDate.AddDays(10), "booster");
        var given = VaccinationEvent.Schedule(Guid.NewGuid(), Guid.NewGuid(), null, "FMD", "", new DateOnly(2026, 3, 1), null);
        given.RecordAdministration(new DateOnly(2026, 3, 1), Guid.NewGuid());

        pending.CancelForAnimalExit(SaleDate, "Closed: animal sold").Should().BeTrue();
        given.CancelForAnimalExit(SaleDate, "Closed: animal sold").Should().BeFalse();

        pending.Status.Should().Be(VaccinationStatus.Cancelled);
        pending.Notes.Should().Contain("booster").And.Contain("Closed: animal sold");
        given.Status.Should().Be(VaccinationStatus.Completed);
    }

    [Fact]
    public void MedicalTreatment_CloseForAnimalExit_EndsOngoingCourseAtExitDate()
    {
        var treatment = MedicalTreatment.LogTreatment(Guid.NewGuid(), Guid.NewGuid(), "Fever", "Oxy",
            Dosage.Create(10, "ml"), WithdrawalPeriod.Create(5, 14), SaleDate.AddDays(-3), null, 500m, null, null);

        treatment.CloseForAnimalExit(SaleDate, "Closed: animal sold").Should().BeTrue();

        treatment.Status.Should().Be(TreatmentStatus.Completed);
        treatment.EndDate.Should().Be(SaleDate);
        treatment.CostBdt.Should().Be(500m);
        treatment.CloseForAnimalExit(SaleDate, "again").Should().BeFalse();
    }
}
