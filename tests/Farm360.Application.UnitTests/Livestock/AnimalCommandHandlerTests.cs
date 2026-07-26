using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.Commands;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Farm360.Application.UnitTests.Livestock;

/// <summary>
/// Unit tests for livestock command handlers.
/// All dependencies (repository, unit-of-work, services) are substituted.
/// Tests verify: handler calls correct domain methods, saves via UoW,
/// throws NotFoundException when entity missing.
/// </summary>
public sealed class AnimalCommandHandlerTests
{
    // ── Shared stubs ──────────────────────────────────────────────────────────
    private readonly IAnimalRepository _repo        = Substitute.For<IAnimalRepository>();
    private readonly IUnitOfWork       _uow         = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ITenantService    _tenantSvc   = Substitute.For<ITenantService>();

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId   = Guid.NewGuid();

    private Animal CreateAnimal() => Animal.Create(
        tenantId:        TenantId,
        farmId:          Guid.NewGuid(),
        tag:             AnimalTag.Create("B-001", TagType.EarTag),
        species:         AnimalSpecies.CattleBeef,
        breedName:       "Shahiwal",
        sex:             AnimalSex.Male,
        dateOfBirth:     new DateOnly(2023, 1, 1),
        acquisitionType: AcquisitionType.Purchased,
        acquisitionDate: new DateOnly(2024, 1, 1),
        acquisitionPriceBdt: 50_000m,
        notes:           null);

    public AnimalCommandHandlerTests()
    {
        _currentUser.UserId.Returns(UserId);
        _tenantSvc.TenantId.Returns(TenantId);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RegisterAnimalCommandHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterAnimal_ValidCommand_AddsToRepositoryAndSaves()
    {
        var handler = new RegisterAnimalCommandHandler(_repo, _uow, _tenantSvc);
        var command = new RegisterAnimalCommand(
            FarmId:              Guid.NewGuid(),
            TagId:               "B-001",
            TagType:             TagType.EarTag,
            Species:             AnimalSpecies.CattleBeef,
            BreedName:           "Shahiwal",
            Sex:                 AnimalSex.Male,
            DateOfBirth:         new DateOnly(2023, 1, 1),
            AcquisitionType:     AcquisitionType.Purchased,
            AcquisitionDate:     new DateOnly(2024, 1, 1),
            AcquisitionPriceBdt: 50_000m,
            Notes:               null);

        var result = await handler.Handle(command, CancellationToken.None);

        _repo.Received(1).Add(Arg.Is<Animal>(a => a.Tag.TagId == "B-001"));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.TagId.Should().Be("B-001");
        result.Status.Should().Be(AnimalStatus.Active);
    }

    [Fact]
    public async Task RegisterAnimal_ValidCommand_ReturnsDtoWithCorrectTenantId()
    {
        var handler = new RegisterAnimalCommandHandler(_repo, _uow, _tenantSvc);
        var command = new RegisterAnimalCommand(
            FarmId:              Guid.NewGuid(),
            TagId:               "B-999",
            TagType:             TagType.Manual,
            Species:             AnimalSpecies.Goat,
            BreedName:           "Black Bengal",
            Sex:                 AnimalSex.Female,
            DateOfBirth:         new DateOnly(2022, 6, 1),
            AcquisitionType:     AcquisitionType.BornOnFarm,
            AcquisitionDate:     new DateOnly(2022, 6, 1),
            AcquisitionPriceBdt: null,
            Notes:               "Born on farm");

        var result = await handler.Handle(command, CancellationToken.None);

        result.TenantId.Should().Be(TenantId);
        result.Species.Should().Be(AnimalSpecies.Goat);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RecordWeightCommandHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RecordWeight_AnimalExists_AddsWeightAndSaves()
    {
        var animal = CreateAnimal();
        _repo.GetByIdWithWeightsAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new RecordWeightCommandHandler(_repo, _uow, _currentUser);
        var command = new RecordWeightCommand(animal.Id, 280m, new DateOnly(2025, 6, 1), null);

        var result = await handler.Handle(command, CancellationToken.None);

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.WeightKg.Should().Be(280m);
        result.AnimalId.Should().Be(animal.Id);
    }

    [Fact]
    public async Task RecordWeight_AnimalNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdWithWeightsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .ReturnsNull();

        var handler = new RecordWeightCommandHandler(_repo, _uow, _currentUser);
        var command = new RecordWeightCommand(Guid.NewGuid(), 280m, new DateOnly(2025, 6, 1), null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SellAnimalCommandHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SellAnimal_AnimalExists_SellsAndSaves()
    {
        var animal = CreateAnimal();
        _repo.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new SellAnimalCommandHandler(_repo, _uow, _currentUser);
        var command = new SellAnimalCommand(animal.Id, 80_000m, new DateOnly(2025, 7, 1), null, null);

        await handler.Handle(command, CancellationToken.None);

        animal.Status.Should().Be(AnimalStatus.Sold);
        animal.SalePriceBdt.Should().Be(80_000m);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SellAnimal_AnimalNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var handler = new SellAnimalCommandHandler(_repo, _uow, _currentUser);
        var act = async () => await handler.Handle(
            new SellAnimalCommand(Guid.NewGuid(), 80_000m, new DateOnly(2025, 7, 1), null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // QuarantineAnimalCommandHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QuarantineAnimal_AnimalExists_QuarantinesAndSaves()
    {
        var animal = CreateAnimal();
        _repo.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new QuarantineAnimalCommandHandler(_repo, _uow);
        await handler.Handle(new QuarantineAnimalCommand(animal.Id, "Suspected FMD"), CancellationToken.None);

        animal.Status.Should().Be(AnimalStatus.Quarantined);
        animal.QuarantineReason.Should().Be("Suspected FMD");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DeleteAnimalCommandHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAnimal_AnimalExists_SoftDeletesAndSaves()
    {
        var animal = CreateAnimal();
        _repo.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new DeleteAnimalCommandHandler(_repo, _uow, _currentUser);
        await handler.Handle(new DeleteAnimalCommand(animal.Id), CancellationToken.None);

        _repo.Received(1).SoftDelete(animal, UserId);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAnimal_AnimalNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var handler = new DeleteAnimalCommandHandler(_repo, _uow, _currentUser);
        var act = async () => await handler.Handle(
            new DeleteAnimalCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
