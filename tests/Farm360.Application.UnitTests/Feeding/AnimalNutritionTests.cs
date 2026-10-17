using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Feeding.Queries.Nutrition;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>Nutrition &amp; growth: intake from the recipe's ingredients, needs from weight, growth from weighings.</summary>
public class AnimalNutritionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 9, 1);

    [Fact]
    public void Needs_FollowWeightAndGain()
    {
        var need = GetAnimalNutritionQueryHandler.Needs(300m, 0.8m)!.Value;

        need.DmKg.Should().Be(7.5m, "2.5% of 300 kg");
        need.CpG.Should().Be(900m, "12% of 7.5 kg DM for 250–400 kg cattle");
        need.MeMj.Should().BeApproximately(34.9m + 28m, 0.5m, "maintenance ≈ 34.9 MJ ME + 0.8 kg gain × 35 MJ");
        GetAnimalNutritionQueryHandler.Needs(null, 0.8m).Should().BeNull();
    }

    [Fact]
    public async Task Intake_IsSplitIntoIngredients_OnADryMatterBasis_AndGrowthComesFromWeighings()
    {
        // Recipe: 80% grain (88% DM, 10% CP, 13 MJ) + 20% grass (20% DM, 12% CP, 8 MJ).
        var recipe = new FeedFormula(Guid.NewGuid(), TenantId, "CP Formula", TargetAnimalType.Cattle);
        recipe.AddIngredient(Guid.NewGuid(), 80m, 30m, new NutritionalProfile(88m, 10m, 13m));
        recipe.AddIngredient(Guid.NewGuid(), 20m, 5m, new NutritionalProfile(20m, 12m, 8m));

        var animal = Farm360.Domain.Livestock.Animal.Create(TenantId, FarmId, AnimalTag.Create("A1", TagType.Manual), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, From.AddYears(-1), AcquisitionType.Purchased, From.AddMonths(-1), 50_000m, null);
        animal.RecordWeight(Weight.Create(300m), From, Guid.NewGuid(), null);
        animal.RecordWeight(Weight.Create(309m), From.AddDays(9), Guid.NewGuid(), null);

        // 10 kg a day for 10 days.
        var fed = Enumerable.Range(0, 10).Select(d => AnimalFeedAllocation.Create(TenantId, animal.Id, FarmId, Guid.NewGuid(), Guid.NewGuid(),
            recipe.Id, null, From.AddDays(d), 10m, 260m, 26m, FeedAllocationOrigin.IndividualPlan, FeedAllocationMethod.EqualPerHead,
            300m, 1m, 1)).ToList();

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        var allocations = Substitute.For<IAnimalFeedAllocationRepository>();
        allocations.GetByAnimalAsync(animal.Id, From, From.AddDays(9), Arg.Any<CancellationToken>()).Returns(fed);
        var formulas = Substitute.For<IFeedFormulaRepository>();
        formulas.GetByIdAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);

        var result = await new GetAnimalNutritionQueryHandler(animals, allocations, formulas)
            .Handle(new GetAnimalNutritionQuery(animal.Id, From, From.AddDays(9)), CancellationToken.None);

        var day = result.Days[0];
        day.DryMatterKg.Should().Be(7.44m, "8 kg × 88% + 2 kg × 20%");
        day.ProteinG.Should().Be(752m, "7.04 kg × 10% + 0.4 kg × 12%, in grams");
        day.EnergyMj.Should().Be(94.7m, "7.04 × 13 + 0.4 × 8");
        result.Days[4].WeightKg.Should().Be(304m, "interpolated between 300 and 309");
        result.Days[4].WeightIsMeasured.Should().BeFalse();

        var s = result.Summary;
        s.GainKg.Should().Be(9m);
        s.AdgKgPerDay.Should().Be(1m);
        s.FeedConversionKgDmPerKgGain.Should().Be(8.3m, "74.4 kg DM ÷ 9 kg gain");
        s.DietProteinPercent.Should().Be(10.1m);
        s.FedDays.Should().Be(10);
    }
}
