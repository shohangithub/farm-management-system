using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Livestock;

/// <summary>
/// The animal list carries shed / pen labels, looked up once per page (not once per shed or animal).
/// </summary>
public sealed class AnimalListLocationNamesTests
{
    private readonly IAnimalRepository _repo = Substitute.For<IAnimalRepository>();
    private readonly IAnimalLocationNames _locations = Substitute.For<IAnimalLocationNames>();

    private static Animal CreateAnimal(string tag, Guid? shedId = null, Guid? penId = null)
    {
        var animal = Animal.Create(Guid.NewGuid(), Guid.NewGuid(), AnimalTag.Create(tag, TagType.EarTag),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male, new DateOnly(2023, 1, 1),
            AcquisitionType.Purchased, new DateOnly(2024, 1, 1), null, null);
        if (shedId is not null || penId is not null)
            animal.TransferToShed(shedId, penId, new DateOnly(2024, 1, 2), Guid.NewGuid());
        return animal;
    }

    private void ReturnPage(params Animal[] animals) =>
        _repo.GetPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            Arg.Any<AnimalSpecies?>(), Arg.Any<AnimalSex?>(),
            Arg.Any<AnimalStatus?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns((animals.ToList(), animals.Length));

    [Fact]
    public async Task GetAnimalList_FillsShedAndPenNames_WithOneLookupPerTable()
    {
        var shedA = Guid.NewGuid();
        var penA1 = Guid.NewGuid();
        var shedB = Guid.NewGuid();
        ReturnPage(
            CreateAnimal("B-001", shedA, penA1),
            CreateAnimal("B-002", shedA, penA1),
            CreateAnimal("B-003", shedB),
            CreateAnimal("B-004"));

        _locations.GetShedNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [shedA] = "Fattening Shed A", [shedB] = "Fattening Shed B" });
        _locations.GetPenNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [penA1] = "A1" });

        var handler = new GetAnimalListQueryHandler(_repo, locations: _locations);
        var result = await handler.Handle(new GetAnimalListQuery(), CancellationToken.None);

        var byTag = result.Items.ToDictionary(i => i.TagId);
        byTag["B-001"].ShedName.Should().Be("Fattening Shed A");
        byTag["B-001"].PenName.Should().Be("A1");
        byTag["B-002"].ShedName.Should().Be("Fattening Shed A");
        byTag["B-003"].ShedName.Should().Be("Fattening Shed B");
        byTag["B-003"].PenName.Should().BeNull();
        byTag["B-004"].ShedName.Should().BeNull();
        byTag["B-004"].PenName.Should().BeNull();

        await _locations.Received(1).GetShedNamesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(shedA) && ids.Contains(shedB)),
            Arg.Any<CancellationToken>());
        await _locations.Received(1).GetPenNamesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(penA1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAnimalList_NoAnimalHoused_SkipsTheLookup()
    {
        ReturnPage(CreateAnimal("B-001"));

        var handler = new GetAnimalListQueryHandler(_repo, locations: _locations);
        var result = await handler.Handle(new GetAnimalListQuery(), CancellationToken.None);

        result.Items.Single().ShedName.Should().BeNull();
        await _locations.DidNotReceiveWithAnyArgs().GetShedNamesAsync(default!, default);
        await _locations.DidNotReceiveWithAnyArgs().GetPenNamesAsync(default!, default);
    }
}
