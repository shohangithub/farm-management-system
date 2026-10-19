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

/// <summary>The feed purchase plan, and units (base unit + packs) on hand-computable numbers.</summary>
public class FeedRequirementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 11, 1);

    // ── Units ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BagsAreStoredAsKg_AndTheRateBecomesCostPerKg()
    {
        var (service, kg, _) = Units();
        var item = Item("Cattle feed", "kg", 0m, 0m);
        var bag = kg.Conversions.Single(c => c.Name == "Bag (50 kg)");

        var q = await service.ToBaseAsync(item, bag.Id, 4m, 2_900m);

        q.BaseQuantity.Should().Be(200m, "4 bags × 50 kg");
        q.BaseUnitCostBdt.Should().Be(58m, "৳2,900 a bag ÷ 50 kg");
        q.EnteredUnitName.Should().Be("Bag (50 kg)");
    }

    [Fact]
    public async Task AUnitOfAnotherBase_IsRejected()
    {
        var (service, kg, _) = Units();
        var syrup = Item("Liver tonic", "ml", 0m, 0m);

        var act = () => service.ToBaseAsync(syrup, kg.Conversions.Single(c => c.Name == "Bag (50 kg)").Id, 1m);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*counted in ml*");
    }

    [Fact]
    public async Task FeedKg_BecomeGramsForAGramItem()
    {
        var (service, _, _) = Units();

        (await service.KgToItemBaseAsync(Item("Premix", "g", 0m, 0m), 0.25m)).Should().Be(250m);
        (await service.KgToItemBaseAsync(Item("Grain", "kg", 0m, 0m), 0.25m)).Should().Be(0.25m);
    }

    [Fact]
    public async Task ItemsWithOnlyAUnitName_AreLinkedToTheirBaseUnitById_CreatingMissingOnes()
    {
        var kg = new MeasureUnit(Guid.NewGuid(), TenantId, "kg");
        var repo = Substitute.For<IMeasureUnitRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<MeasureUnit> { kg });
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);
        var grain = Item("Maize", "Kgs", 0m, 0m);
        var vial = Item("Vaccine", "vial", 0m, 0m);
        var items = Substitute.For<IInventoryItemRepository>();
        items.GetWithoutBaseUnitAsync(Arg.Any<CancellationToken>()).Returns(new List<InventoryItem> { grain, vial });

        var service = new UnitService(repo, tenant, Substitute.For<IUnitOfWork>(), items);
        var all = await service.GetUnitsAsync();

        grain.BaseUnitId.Should().Be(kg.Id, "\"Kgs\" is a spelling of kg");
        grain.UnitOfMeasure.Should().Be("kg");
        var created = all.Single(u => u.Name == "vial");
        vial.BaseUnitId.Should().Be(created.Id, "a unit name with no base unit gets one");
        await repo.Received(1).AddAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AConversionOfAnotherBaseUnit_IsRejected_EvenWhenNamesLookAlike()
    {
        var (service, kg, g) = Units();
        var premix = Item("Premix", "g", 0m, 0m);
        premix.SetBaseUnit(g.Id, g.Name);

        var act = () => service.ToBaseAsync(premix, kg.Conversions.Single(c => c.Name == "Bag (25 kg)").Id, 1m);

        await act.Should().ThrowAsync<ArgumentException>();
        (await service.ToBaseAsync(premix, g.Conversions.Single(c => c.Name == "Kg").Id, 0.5m)).BaseQuantity.Should().Be(500m);
    }

    // ── Purchase plan ──────────────────────────────────────────────────────

    [Fact]
    public async Task TenDays_OfAFixedChart_ExplodeIntoItems_InTheirOwnUnits_AndBags()
    {
        var (units, kg, _) = Units();
        var grain = Item("Maize", "kg", stock: 5m, wac: 40m);
        grain.SetDefaultUnit(kg.Conversions.Single(c => c.Name == "Bag (25 kg)").Id);
        var premix = Item("Premix", "g", stock: 0m, wac: 0.5m);

        var grainIng = new FeedIngredient(Guid.NewGuid(), TenantId, "Maize", FeedCategory.Concentrate, NutritionalProfile.Empty, 40m, inventoryItemId: grain.Id);
        var premixIng = new FeedIngredient(Guid.NewGuid(), TenantId, "Premix", FeedCategory.Additive, NutritionalProfile.Empty, 500m, inventoryItemId: premix.Id);
        var formula = new FeedFormula(Guid.NewGuid(), TenantId, "CP Formula", TargetAnimalType.Cattle);
        formula.AddIngredient(grainIng.Id, 99m, 40m, NutritionalProfile.Empty);
        formula.AddIngredient(premixIng.Id, 1m, 500m, NutritionalProfile.Empty);

        var chart = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Fixed 2 kg", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
        chart.AddRuleLine(null, null, null, null, FeedCategory.Concentrate, 2m, formula.Id);

        var a1 = Animal("A1");
        var a2 = Animal("A2");
        var plans = new List<AnimalFeedingPlan>
        {
            new(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.FixedQuantity, From.AddDays(-10), null, animalId: a1.Id),
            new(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.FixedQuantity, From.AddDays(-10), null, animalId: a2.Id),
        };

        var result = await Handler(units, [a1, a2], plans, [chart], [formula], [grainIng, premixIng], [grain, premix])
            .Handle(new GetFeedRequirementQuery(FarmId, 10, From, AnimalIds: [a1.Id]), CancellationToken.None);

        result.Animals.Should().Be(1, "only A1 was asked for");
        var maize = result.Items.Single(i => i.Name == "Maize");
        maize.Total.Should().Be(19.8m, "2 kg × 10 days × 99%");
        maize.ToBuy.Should().Be(14.8m, "5 kg already in stock");
        maize.PacksToBuy.Should().Be(1, "one 25 kg bag covers 14.8 kg");
        maize.CostToBuyBdt.Should().Be(592m, "14.8 kg × ৳40");

        var px = result.Items.Single(i => i.Name == "Premix");
        px.BaseUnit.Should().Be("g");
        px.Total.Should().Be(200m, "0.2 kg of premix is 200 g");

        result.Recipes.Single().TotalKg.Should().Be(20m);
        result.PerAnimal.Single().PerDayKg.Should().Be(2m);
    }

    [Fact]
    public async Task PausedDays_AndPlanEnds_AreNotCounted()
    {
        var (units, _, _) = Units();
        var grain = Item("Maize", "kg", 0m, 40m);
        var ing = new FeedIngredient(Guid.NewGuid(), TenantId, "Maize", FeedCategory.Concentrate, NutritionalProfile.Empty, 40m, inventoryItemId: grain.Id);
        var formula = new FeedFormula(Guid.NewGuid(), TenantId, "Maize only", TargetAnimalType.Cattle);
        formula.AddIngredient(ing.Id, 100m, 40m, NutritionalProfile.Empty);
        var chart = new FeedingRuleSet(Guid.NewGuid(), TenantId, "1 kg", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
        chart.AddRuleLine(null, null, null, null, FeedCategory.Concentrate, 1m, formula.Id);
        var a = Animal("A1");
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, chart.Id, FeedingPlanType.FixedQuantity, From, From.AddDays(7), animalId: a.Id);
        plan.AddExclusion(From.AddDays(2), "Sick", From.AddDays(4)); // days 3 and 4 off

        var result = await Handler(units, [a], [plan], [chart], [formula], [ing], [grain])
            .Handle(new GetFeedRequirementQuery(FarmId, 15, From), CancellationToken.None);

        result.Items.Single().Total.Should().Be(6m, "8 days in the plan, 2 of them paused");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (UnitService Service, MeasureUnit Kg, MeasureUnit G) Units()
    {
        var kg = new MeasureUnit(Guid.NewGuid(), TenantId, "kg");
        kg.AddConversion("Bag (25 kg)", 25m);
        kg.AddConversion("Bag (50 kg)", 50m);
        var g = new MeasureUnit(Guid.NewGuid(), TenantId, "g");
        g.AddConversion("Kg", 1000m);
        var ml = new MeasureUnit(Guid.NewGuid(), TenantId, "ml");

        var repo = Substitute.For<IMeasureUnitRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<MeasureUnit> { kg, g, ml });
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);
        return (new UnitService(repo, tenant, Substitute.For<IUnitOfWork>()), kg, g);
    }

    private static InventoryItem Item(string name, string unit, decimal stock, decimal wac) =>
        new(Guid.NewGuid(), TenantId, FarmId, name, string.Empty, InventoryCategory.Feed, unit, 0m, stock, wac);

    private static Animal Animal(string tag) =>
        Farm360.Domain.Livestock.Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.Manual), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, From.AddYears(-1), AcquisitionType.Purchased, From.AddMonths(-1), 50_000m, null);

    private static GetFeedRequirementQueryHandler Handler(
        UnitService units, List<Animal> herd, List<AnimalFeedingPlan> plans, List<FeedingRuleSet> sets,
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
        var cons = Substitute.For<IConsumableUsagePlanRepository>();
        cons.GetByFarmIdAsync(FarmId, true, Arg.Any<CancellationToken>()).Returns(new List<ConsumableUsagePlan>());
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);

        return new GetFeedRequirementQueryHandler(animals, planRepo, setRepo, formulaRepo, ingRepo, inv, cons,
            Substitute.For<IShedRepository>(), Substitute.For<IPenRepository>(), Substitute.For<IAnimalBatchRepository>(), units, tenant);
    }
}
