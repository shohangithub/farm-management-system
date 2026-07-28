using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Exceptions;
using Farm360.Domain.Feeding.ValueObjects;
using Xunit;

namespace Farm360.Domain.UnitTests.Feeding;

public class FeedingDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    [Fact]
    public void CreateFeedIngredient_ShouldSetPropertiesCorrectly()
    {
        var profile = new NutritionalProfile(88.0m, 18.0m, 11.5m, 8.0m, 0.5m, 0.3m);
        var ingredient = new FeedIngredient(
            Guid.NewGuid(), _tenantId, "Maize Silage", FeedCategory.Silage, profile, 45.0m, "kg", false, "Test Silage");

        Assert.Equal("Maize Silage", ingredient.Name);
        Assert.Equal(FeedCategory.Silage, ingredient.Category);
        Assert.Equal(45.0m, ingredient.UnitCostBdt);
        Assert.True(ingredient.IsActive);
        Assert.Equal(88.0m, ingredient.NutritionalProfile.DryMatterPercentage);
    }

    [Fact]
    public void CreateFeedFormula_WithIngredients_ShouldRecalculateNutritionalTotals()
    {
        var formula = new FeedFormula(Guid.NewGuid(), _tenantId, "Milking Ration", TargetAnimalType.Cattle, "Peak Milk");

        var profile1 = new NutritionalProfile(90.0m, 20.0m, 12.0m);
        var profile2 = new NutritionalProfile(85.0m, 10.0m, 10.0m);

        formula.AddIngredient(Guid.NewGuid(), 60.0m, 50.0m, profile1);
        formula.AddIngredient(Guid.NewGuid(), 40.0m, 30.0m, profile2);

        // 60% of 50 + 40% of 30 = 30 + 12 = 42 BDT/kg
        Assert.Equal(42.0m, formula.TotalCostPerKgBdt);
        Assert.Equal(2, formula.Ingredients.Count);
    }

    [Fact]
    public void SetFormulaStatus_ToActiveWithoutIngredients_ShouldThrowFeedingDomainException()
    {
        var formula = new FeedFormula(Guid.NewGuid(), _tenantId, "Empty Formula", TargetAnimalType.Cattle);

        Assert.Throws<FeedingDomainException>(() => formula.SetStatus(FormulaStatus.Active));
    }

    [Fact]
    public void CreateFeedConsumptionLog_ShouldCalculateNetConsumptionAndTotalCost()
    {
        var formulaId = Guid.NewGuid();
        var log = new FeedConsumptionLog(
            Guid.NewGuid(), _tenantId, _farmId, formulaId, DateOnly.FromDateTime(DateTime.UtcNow),
            20, 200.0m, 10.0m, 40.0m);

        Assert.Equal(190.0m, log.NetConsumptionKg);
        Assert.Equal(7600.0m, log.TotalCostBdt);
        Assert.Single(log.DomainEvents);
    }
}
