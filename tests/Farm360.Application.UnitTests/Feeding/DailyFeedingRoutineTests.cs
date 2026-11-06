using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Queries.Planning;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>The daily routine: chart band per animal, mixing sheet and feeding times.</summary>
public class DailyFeedingRoutineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 1);

    [Fact]
    public async Task EachAnimalGetsItsWeightBand_AndTheRecipeIsSplitIntoIngredientsAndFeedingTimes()
    {
        var maize = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, "Maize", string.Empty, InventoryCategory.Feed, "kg", 0m, 3m, 40m);
        var maizeIng = new FeedIngredient(Guid.NewGuid(), TenantId, "Maize", FeedCategory.Concentrate, NutritionalProfile.Empty, 40m, inventoryItemId: maize.Id);
        var bran = new FeedIngredient(Guid.NewGuid(), TenantId, "Wheat bran", FeedCategory.Concentrate, NutritionalProfile.Empty, 30m);
        var recipe = new FeedFormula(Guid.NewGuid(), TenantId, "CP Formula", TargetAnimalType.Cattle);
        recipe.AddIngredient(maizeIng.Id, 60m, 40m, NutritionalProfile.Empty);
        recipe.AddIngredient(bran.Id, 40m, 30m, NutritionalProfile.Empty);

        var chart = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Fattening", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.WeightQuantity);
        chart.AddRuleLine(100m, 200m, recipe.Id, 2m, 0m, sessionsPerDay: 2);
        chart.AddRuleLine(200m, 300m, recipe.Id, 4m, 0m, sessionsPerDay: 2);

        var small = Animal("A1", 150m);
        var big = Animal("A2", 250m);
        var noRation = Animal("A3", 200m);
        var plans = new List<AnimalFeedingPlan>
        {
            new(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.WeightQuantity, Day.AddDays(-5), null, animalId: small.Id),
            new(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.WeightQuantity, Day.AddDays(-5), null, animalId: big.Id),
        };

        var result = await Handler([small, big, noRation], plans, [chart], [recipe], [maizeIng, bran], [maize])
            .Handle(new GetDailyFeedingRoutineQuery(FarmId, Day), CancellationToken.None);

        result.Animals.Should().Be(2);
        result.AnimalsWithoutRation.Should().Be(1);
        var a1 = result.PerAnimal.Single(a => a.Tag == "A1").Lines.Single();
        a1.Band.Should().Be("100–200 kg");
        a1.KgPerDay.Should().Be(2m);
        a1.KgPerSession.Should().Be(1m);
        result.PerAnimal.Single(a => a.Tag == "A2").Lines.Single().Band.Should().Be("200–300 kg");

        var mix = result.Recipes.Single();
        mix.TotalKg.Should().Be(6m, "2 kg + 4 kg");
        mix.Sessions.Select(s => (s.Session, s.Kg)).Should().Equal(("Morning", 3m), ("Evening", 3m));
        var m = mix.Ingredients.Single(i => i.Ingredient == "Maize");
        m.Kg.Should().Be(3.6m, "60% of 6 kg");
        m.IsShort.Should().BeTrue("3.6 kg needed, 3 kg in stock");
        mix.Ingredients.Single(i => i.Ingredient == "Wheat bran").StockItem.Should().BeNull("not linked to stock");
    }

    [Fact]
    public async Task AChosenChart_ShowsOnlyItsAnimals_AndCanFeedAnimalsWithoutARation()
    {
        var recipe = new FeedFormula(Guid.NewGuid(), TenantId, "CP Formula", TargetAnimalType.Cattle);
        var chart = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Fattening", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.WeightQuantity);
        chart.AddRuleLine(100m, 200m, recipe.Id, 2m, 0m, sessionsPerDay: 1);
        chart.AddRuleLine(200m, 300m, recipe.Id, 4m, 0m, sessionsPerDay: 1);
        var other = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Dairy", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
        other.AddRuleLine(null, null, null, null, FeedCategory.Concentrate, 5m, recipe.Id);

        var onChart = Animal("A1", 150m);
        var onOther = Animal("A2", 250m);
        var none = Animal("A3", 250m);
        var plans = new List<AnimalFeedingPlan>
        {
            new(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.WeightQuantity, Day.AddDays(-5), null, animalId: onChart.Id),
            new(Guid.NewGuid(), TenantId, FarmId, other.Id, FeedingPlanType.FixedQuantity, Day.AddDays(-5), null, animalId: onOther.Id),
        };
        var handler = Handler([onChart, onOther, none], plans, [chart, other], [recipe], [], []);

        var only = await handler.Handle(new GetDailyFeedingRoutineQuery(FarmId, Day, RuleSetId: chart.Id), CancellationToken.None);
        only.PerAnimal.Select(a => a.Tag).Should().Equal("A1");
        only.AnimalsWithoutRation.Should().Be(1, "A3 has no ration; A2 is on another chart");

        var applied = await handler.Handle(new GetDailyFeedingRoutineQuery(FarmId, Day, RuleSetId: chart.Id, ApplyToUnassigned: true), CancellationToken.None);
        applied.PerAnimal.Select(a => a.Tag).Should().Equal("A1", "A3");
        var a3 = applied.PerAnimal.Single(a => a.Tag == "A3").Lines.Single();
        a3.Kind.Should().Be("Not assigned");
        a3.Band.Should().Be("200–300 kg");
        a3.KgPerDay.Should().Be(4m);
        applied.Recipes.Single().TotalKg.Should().Be(6m);
    }

    private static Animal Animal(string tag, decimal weightKg)
    {
        var a = Farm360.Domain.Livestock.Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.Manual), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, Day.AddYears(-1), AcquisitionType.Purchased, Day.AddMonths(-1), 50_000m, null);
        a.RecordWeight(Weight.Create(weightKg), Day, Guid.NewGuid(), null);
        return a;
    }

    private static GetDailyFeedingRoutineQueryHandler Handler(
        List<Animal> herd, List<AnimalFeedingPlan> plans, List<FeedingRuleSet> sets,
        List<FeedFormula> formulas, List<FeedIngredient> ingredients, List<InventoryItem> items)
    {
        var animals = Substitute.For<IAnimalRepository>();
        animals.GetAllByFarmAsync(FarmId, Arg.Any<CancellationToken>()).Returns(herd);
        animals.GetCurrentLocationsByFarmAsync(FarmId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, (Guid? ShedId, Guid? PenId)>());
        var planRepo = Substitute.For<IAnimalFeedingPlanRepository>();
        planRepo.GetActivePlansByFarmAsync(TenantId, FarmId, Arg.Any<CancellationToken>()).Returns(plans);
        var setRepo = Substitute.For<IFeedingRuleSetRepository>();
        setRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(sets);
        var formulaRepo = Substitute.For<IFeedFormulaRepository>();
        formulaRepo.GetListAsync(TenantId, 1, 2000, null, Arg.Any<CancellationToken>()).Returns(formulas);
        var ingRepo = Substitute.For<IFeedIngredientRepository>();
        ingRepo.GetAllAsync(TenantId, true, Arg.Any<CancellationToken>()).Returns(ingredients);
        var inv = Substitute.For<IInventoryItemRepository>();
        inv.GetByFarmIdAsync(FarmId, null, Arg.Any<CancellationToken>()).Returns(items);
        var units = Substitute.For<IUnitService>();
        units.KgToItemBaseAsync(Arg.Any<InventoryItem>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<decimal>(1));
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);

        return new GetDailyFeedingRoutineQueryHandler(animals, planRepo, setRepo, formulaRepo, ingRepo, inv,
            Substitute.For<IShedRepository>(), Substitute.For<IPenRepository>(), Substitute.For<IAnimalBatchRepository>(), units, tenant);
    }
}
