using System;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;

namespace Farm360.Application.UnitTests.Intelligence;

internal sealed class TestRulesProvider : IFarmBusinessRulesProvider
{
    public FarmBusinessRules Rules { get; } = new();
    public FarmBusinessRules Current => Rules;
}

internal static class TestAnimals
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid FarmId = Guid.NewGuid();

    public static Animal Create(decimal? acquisitionPrice, decimal? weightKg, string tag = "T-1")
    {
        var animal = Animal.Create(
            tenantId: TenantId,
            farmId: FarmId,
            tag: AnimalTag.Create(tag, TagType.EarTag),
            species: AnimalSpecies.CattleBeef,
            breedId: Guid.NewGuid(),
            sex: AnimalSex.Male,
            dateOfBirth: new DateOnly(2024, 1, 1),
            acquisitionType: AcquisitionType.Purchased,
            acquisitionDate: new DateOnly(2025, 1, 1),
            acquisitionPriceBdt: acquisitionPrice,
            notes: null);

        if (weightKg is { } w)
        {
            animal.RecordWeight(Weight.Create(w), new DateOnly(2025, 6, 1), Guid.NewGuid(), null);
        }

        return animal;
    }
}

/// <summary>A real <see cref="Farm360.Application.Intelligence.Pricing.AnimalPriceResolver"/> over in-memory animals and breeds.</summary>
internal static class TestPrices
{
    public static Farm360.Application.Intelligence.Pricing.AnimalPriceResolver Resolver(
        IFarmBusinessRulesProvider rules,
        System.Collections.Generic.IReadOnlyList<Animal>? farmAnimals = null,
        System.Collections.Generic.IReadOnlyList<Breed>? breeds = null,
        Guid? farmId = null)
    {
        var animals = NSubstitute.Substitute.For<Farm360.Domain.Livestock.Repositories.IAnimalRepository>();
        NSubstitute.SubstituteExtensions.Returns(
            animals.GetAllByFarmAsync(farmId ?? TestAnimals.FarmId, NSubstitute.Arg.Any<System.Threading.CancellationToken>()),
            System.Threading.Tasks.Task.FromResult(farmAnimals ?? Array.Empty<Animal>()));
        var breedRepo = NSubstitute.Substitute.For<Farm360.Domain.Livestock.Repositories.IBreedRepository>();
        NSubstitute.SubstituteExtensions.Returns(
            breedRepo.GetAllAsync(NSubstitute.Arg.Any<System.Threading.CancellationToken>()),
            System.Threading.Tasks.Task.FromResult(breeds ?? Array.Empty<Breed>()));
        return new Farm360.Application.Intelligence.Pricing.AnimalPriceResolver(animals, breedRepo, rules);
    }
}
