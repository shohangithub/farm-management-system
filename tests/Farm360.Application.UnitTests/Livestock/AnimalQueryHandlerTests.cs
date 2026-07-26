using Farm360.Application.Common.Exceptions;
using Farm360.Application.Livestock.Queries;
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
/// Unit tests for livestock query handlers.
/// All read-side handlers are pure: they call the repository and map — no side effects.
/// </summary>
public sealed class AnimalQueryHandlerTests
{
    private readonly IAnimalRepository _repo = Substitute.For<IAnimalRepository>();

    private static Animal CreateAnimal(string tagId = "B-001") => Animal.Create(
        tenantId:        Guid.NewGuid(),
        farmId:          Guid.NewGuid(),
        tag:             AnimalTag.Create(tagId, TagType.EarTag),
        species:         AnimalSpecies.CattleBeef,
        breedName:       "Shahiwal",
        sex:             AnimalSex.Male,
        dateOfBirth:     new DateOnly(2023, 1, 1),
        acquisitionType: AcquisitionType.Purchased,
        acquisitionDate: new DateOnly(2024, 1, 1),
        acquisitionPriceBdt: null,
        notes:           null);

    // ══════════════════════════════════════════════════════════════════════════
    // GetAnimalByIdQueryHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAnimalById_AnimalExists_ReturnsDto()
    {
        var animal = CreateAnimal();
        _repo.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new GetAnimalByIdQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalByIdQuery(animal.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(animal.Id);
        result.TagId.Should().Be("B-001");
        result.Species.Should().Be(AnimalSpecies.CattleBeef);
        result.Status.Should().Be(AnimalStatus.Active);
    }

    [Fact]
    public async Task GetAnimalById_AnimalNotFound_ReturnsNull()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var handler = new GetAnimalByIdQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAnimalById_AnimalExists_MapsAllFields()
    {
        var animal = CreateAnimal("TAG-XYZ");
        _repo.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new GetAnimalByIdQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalByIdQuery(animal.Id), CancellationToken.None);

        result!.BreedName.Should().Be("Shahiwal");
        result.WeightRecords.Should().BeEmpty();
        result.BreedingRecords.Should().BeEmpty();
        result.Photos.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GetAnimalListQueryHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAnimalList_ReturnsPagedResult()
    {
        var animals = new List<Animal> { CreateAnimal("B-001"), CreateAnimal("B-002") };
        _repo.GetPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(),
            Arg.Any<AnimalStatus?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns((animals, 2));

        var handler = new GetAnimalListQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalListQuery(PageNumber: 1, PageSize: 20), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAnimalList_PageSizeExceeds100_ClampsTo100()
    {
        _repo.GetPagedAsync(
            Arg.Any<int>(), Arg.Is<int>(ps => ps == 100), // clamped
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(),
            Arg.Any<AnimalStatus?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns((new List<Animal>(), 0));

        var handler = new GetAnimalListQueryHandler(_repo);
        await handler.Handle(new GetAnimalListQuery(PageSize: 999), CancellationToken.None);

        await _repo.Received(1).GetPagedAsync(
            Arg.Any<int>(), 100,
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(),
            Arg.Any<AnimalStatus?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAnimalList_PageNumberZero_ClampsTo1()
    {
        _repo.GetPagedAsync(
            Arg.Is<int>(pn => pn == 1), Arg.Any<int>(),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(),
            Arg.Any<AnimalStatus?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns((new List<Animal>(), 0));

        var handler = new GetAnimalListQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalListQuery(PageNumber: 0), CancellationToken.None);

        result.PageNumber.Should().Be(1);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GetAnimalWeightHistoryQueryHandler
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetWeightHistory_AnimalExists_ReturnsChronologicalWeights()
    {
        var animal = CreateAnimal();
        var userId = Guid.NewGuid();

        // Add weights out of chronological order
        animal.RecordWeight(Weight.Create(200m), new DateOnly(2025, 1, 1), userId, null);
        animal.RecordWeight(Weight.Create(220m), new DateOnly(2025, 3, 1), userId, null);
        animal.RecordWeight(Weight.Create(210m), new DateOnly(2025, 2, 1), userId, null);

        _repo.GetByIdWithWeightsAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var handler = new GetAnimalWeightHistoryQueryHandler(_repo);
        var result  = await handler.Handle(new GetAnimalWeightHistoryQuery(animal.Id), CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].RecordedDate.Should().Be(new DateOnly(2025, 1, 1)); // earliest first
        result[1].RecordedDate.Should().Be(new DateOnly(2025, 2, 1));
        result[2].RecordedDate.Should().Be(new DateOnly(2025, 3, 1));
    }

    [Fact]
    public async Task GetWeightHistory_AnimalNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdWithWeightsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .ReturnsNull();

        var handler = new GetAnimalWeightHistoryQueryHandler(_repo);
        var act = async () => await handler.Handle(
            new GetAnimalWeightHistoryQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
