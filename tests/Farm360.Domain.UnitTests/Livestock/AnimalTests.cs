using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Livestock;

/// <summary>
/// Unit tests for the Animal aggregate root.
/// Tests domain logic only — no persistence, no DI.
/// Constitution §5 (DDD): Aggregate invariants are enforced inside the domain.
/// Naming convention: Method_Scenario_Expected
/// </summary>
public sealed class AnimalTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId   = Guid.NewGuid();
    private static readonly AnimalTag DefaultTag = AnimalTag.Create("B-001", TagType.EarTag);

    private static Animal CreateActiveAnimal(
        AnimalTag?     tag       = null,
        DateOnly?      dob       = null,
        DateOnly?      acqDate   = null,
        AnimalSpecies  species   = AnimalSpecies.CattleBeef,
        AnimalSex      sex       = AnimalSex.Male) =>
        Animal.Create(
            tenantId:           TenantId,
            farmId:             FarmId,
            tag:                tag ?? DefaultTag,
            species:            species,
            breedName:          "Shahiwal",
            sex:                sex,
            dateOfBirth:        dob ?? new DateOnly(2023, 1, 15),
            acquisitionType:    AcquisitionType.Purchased,
            acquisitionDate:    acqDate ?? new DateOnly(2024, 1, 1),
            acquisitionPriceBdt: 50_000m,
            notes:              null);

    // ══════════════════════════════════════════════════════════════════════════
    // Animal.Create
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Create_ValidData_ReturnsActiveAnimal()
    {
        var animal = CreateActiveAnimal();

        animal.Status.Should().Be(AnimalStatus.Active);
        animal.Id.Should().NotBeEmpty();
        animal.TenantId.Should().Be(TenantId);
        animal.FarmId.Should().Be(FarmId);
        animal.Tag.TagId.Should().Be("B-001");
        animal.Tag.TagType.Should().Be(TagType.EarTag);
        animal.BreedName.Should().Be("Shahiwal");
        animal.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_ValidData_RaisesAnimalRegisteredEvent()
    {
        var animal = CreateActiveAnimal();

        animal.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "AnimalRegisteredEvent");
    }

    [Fact]
    public void Create_EmptyTenantId_ThrowsArgumentException()
    {
        var act = () => Animal.Create(
            tenantId:        Guid.Empty,
            farmId:          FarmId,
            tag:             DefaultTag,
            species:         AnimalSpecies.CattleBeef,
            breedName:       "Test",
            sex:             AnimalSex.Male,
            dateOfBirth:     new DateOnly(2023, 1, 1),
            acquisitionType: AcquisitionType.Purchased,
            acquisitionDate: new DateOnly(2024, 1, 1),
            acquisitionPriceBdt: null,
            notes:           null);

        act.Should().Throw<ArgumentException>().WithMessage("*TenantId*");
    }

    [Fact]
    public void Create_EmptyFarmId_DoesNotThrow_FarmIdIsNotValidatedInDomain()
    {
        // Animal.Create() does not validate FarmId — that is the Application layer's
        // responsibility (FluentValidation on the command). Domain only validates TenantId.
        var act = () => Animal.Create(
            tenantId:        TenantId,
            farmId:          Guid.Empty,
            tag:             DefaultTag,
            species:         AnimalSpecies.CattleBeef,
            breedName:       "Test",
            sex:             AnimalSex.Male,
            dateOfBirth:     new DateOnly(2023, 1, 1),
            acquisitionType: AcquisitionType.Purchased,
            acquisitionDate: new DateOnly(2024, 1, 1),
            acquisitionPriceBdt: null,
            notes:           null);

        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RecordWeight
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordWeight_ValidWeight_AddsWeightRecord()
    {
        var animal = CreateActiveAnimal();
        var weight = Weight.Create(250m);
        var date   = new DateOnly(2025, 6, 1);
        var userId = Guid.NewGuid();

        animal.RecordWeight(weight, date, userId, notes: null);

        animal.WeightRecords.Should().HaveCount(1);
        animal.WeightRecords.First().Weight.WeightKg.Should().Be(250m);
        animal.WeightRecords.First().RecordedDate.Should().Be(date);
        animal.LatestWeightKg.Should().Be(250m);
        animal.LatestWeightDate.Should().Be(date);
    }

    [Fact]
    public void RecordWeight_TwoWeights_ComputesAdgCorrectly()
    {
        var animal = CreateActiveAnimal(dob: new DateOnly(2024, 1, 1));
        var userId = Guid.NewGuid();

        animal.RecordWeight(Weight.Create(200m), new DateOnly(2025, 1, 1), userId, null);
        animal.RecordWeight(Weight.Create(210m), new DateOnly(2025, 2, 1), userId, null);

        // ADG = (210 - 200) / 31 days ≈ 0.323
        animal.AdgKgPerDay.Should().BeApproximately(10m / 31m, precision: 0.001m);
        animal.LatestWeightKg.Should().Be(210m);
    }

    [Fact]
    public void RecordWeight_OnSoldAnimal_ThrowsInvalidOperationException()
    {
        var animal = CreateActiveAnimal();
        animal.Sell(80_000m, new DateOnly(2025, 3, 1), Guid.NewGuid(), null, null);

        var act = () => animal.RecordWeight(Weight.Create(300m), new DateOnly(2025, 3, 2), Guid.NewGuid(), null);

        act.Should().Throw<InvalidAnimalStateTransitionException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Sell
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sell_ActiveAnimal_TransitionsToSold()
    {
        var animal    = CreateActiveAnimal();
        var saleDate  = new DateOnly(2025, 5, 1);
        var salePrice = 75_000m;

        animal.Sell(salePrice, saleDate, Guid.NewGuid(), null, null);

        animal.Status.Should().Be(AnimalStatus.Sold);
        animal.SalePriceBdt.Should().Be(salePrice);
        animal.SaleDate.Should().Be(saleDate);
    }

    [Fact]
    public void Sell_ActiveAnimal_RaisesAnimalSoldEvent()
    {
        var animal = CreateActiveAnimal();
        animal.Sell(75_000m, new DateOnly(2025, 5, 1), Guid.NewGuid(), null, null);

        animal.DomainEvents.Should().Contain(e => e.GetType().Name == "AnimalSoldEvent");
    }

    [Fact]
    public void Sell_QuarantinedAnimal_ThrowsAnimalQuarantinedException()
    {
        var animal = CreateActiveAnimal();
        animal.Quarantine("Foot and mouth");

        var act = () => animal.Sell(75_000m, new DateOnly(2025, 5, 1), Guid.NewGuid(), null, null);

        act.Should().Throw<AnimalQuarantinedException>();
    }

    [Fact]
    public void Sell_AlreadySoldAnimal_ThrowsInvalidStateException()
    {
        var animal = CreateActiveAnimal();
        animal.Sell(75_000m, new DateOnly(2025, 5, 1), Guid.NewGuid(), null, null);

        var act = () => animal.Sell(80_000m, new DateOnly(2025, 5, 2), Guid.NewGuid(), null, null);

        act.Should().Throw<InvalidAnimalStateTransitionException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Quarantine / Release
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Quarantine_ActiveAnimal_TransitionsToQuarantined()
    {
        var animal = CreateActiveAnimal();

        animal.Quarantine("Suspected disease");

        animal.Status.Should().Be(AnimalStatus.Quarantined);
        animal.QuarantineReason.Should().Be("Suspected disease");
    }

    [Fact]
    public void Quarantine_EmptyReason_ThrowsArgumentException()
    {
        var animal = CreateActiveAnimal();

        var act = () => animal.Quarantine(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReleaseFromQuarantine_QuarantinedAnimal_TransitionsToActive()
    {
        var animal = CreateActiveAnimal();
        animal.Quarantine("Test reason");

        animal.ReleaseFromQuarantine();

        animal.Status.Should().Be(AnimalStatus.Active);
        animal.QuarantineReason.Should().BeNull();
    }

    [Fact]
    public void ReleaseFromQuarantine_ActiveAnimal_ThrowsInvalidStateException()
    {
        var animal = CreateActiveAnimal();

        var act = () => animal.ReleaseFromQuarantine();

        act.Should().Throw<InvalidAnimalStateTransitionException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RecordDeath
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordDeath_ActiveAnimal_TransitionsToDead()
    {
        var animal    = CreateActiveAnimal();
        var deathDate = new DateOnly(2025, 7, 1);

        animal.RecordDeath(DisposalReason.NaturalDeath, deathDate, notes: null);

        animal.Status.Should().Be(AnimalStatus.Dead);
        animal.DisposalReason.Should().Be(DisposalReason.NaturalDeath);
    }

    [Fact]
    public void RecordDeath_SoldAnimal_ThrowsInvalidStateException()
    {
        var animal = CreateActiveAnimal();
        animal.Sell(50_000m, new DateOnly(2025, 5, 1), Guid.NewGuid(), null, null);

        var act = () => animal.RecordDeath(DisposalReason.Disease, new DateOnly(2025, 5, 2), null);

        act.Should().Throw<InvalidAnimalStateTransitionException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TransferToShed
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TransferToShed_ActiveAnimal_UpdatesShedId()
    {
        var animal  = CreateActiveAnimal();
        var newShed = Guid.NewGuid();

        animal.TransferToShed(newShed, null, new DateOnly(2025, 4, 1), Guid.NewGuid());

        animal.CurrentMovement?.ShedId.Should().Be(newShed);
    }

    [Fact]
    public void TransferToShed_NullShed_ClearsShed()
    {
        var animal = CreateActiveAnimal();
        animal.TransferToShed(Guid.NewGuid(), null, new DateOnly(2025, 4, 1), Guid.NewGuid());

        animal.TransferToShed(null, null, new DateOnly(2025, 4, 2), Guid.NewGuid());

        animal.CurrentMovement?.ShedId.Should().BeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AddPhoto
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddPhoto_FirstPhoto_IsMarkedPrimary()
    {
        var animal = CreateActiveAnimal();

        var photo = animal.AddPhoto("https://cdn.farm360.ai/photo1.jpg", "Front view", Guid.NewGuid());

        photo.IsPrimary.Should().BeTrue();
        animal.Photos.Should().HaveCount(1);
    }

    [Fact]
    public void AddPhoto_SecondPhoto_IsNotPrimary()
    {
        var animal = CreateActiveAnimal();
        animal.AddPhoto("https://cdn.farm360.ai/photo1.jpg", null, Guid.NewGuid());

        var second = animal.AddPhoto("https://cdn.farm360.ai/photo2.jpg", "Side view", Guid.NewGuid());

        second.IsPrimary.Should().BeFalse();
        animal.Photos.Should().HaveCount(2);
    }

    [Fact]
    public void AddPhoto_EmptyUrl_ThrowsArgumentException()
    {
        var animal = CreateActiveAnimal();

        var act = () => animal.AddPhoto(string.Empty, null, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SoftDelete
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SoftDelete_ActiveAnimal_SetsIsDeletedTrue()
    {
        var animal = CreateActiveAnimal();
        var userId = Guid.NewGuid();

        animal.SoftDelete(userId);

        animal.IsDeleted.Should().BeTrue();
        animal.DeletedBy.Should().Be(userId);
        animal.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void SoftDelete_IsIdempotent_DoesNotThrowOnDoubleCall()
    {
        // AuditableEntity.SoftDelete() does not guard against double-deletion —
        // it simply overwrites. This is by design (last-writer wins on audit fields).
        var animal = CreateActiveAnimal();
        animal.SoftDelete(Guid.NewGuid());

        var act = () => animal.SoftDelete(Guid.NewGuid());

        act.Should().NotThrow();
        animal.IsDeleted.Should().BeTrue();
    }
}
