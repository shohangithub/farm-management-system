using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.ValueObjects;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Xunit;

namespace Farm360.Domain.UnitTests.Feeding;

public class FeedQuantityRulesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void ShareOf_NormalisesBySumOfPercentages()
    {
        var formula = new FeedFormula(Guid.NewGuid(), _tenantId, "Mix", TargetAnimalType.Cattle);
        formula.AddIngredient(Guid.NewGuid(), 60m, 30m, NutritionalProfile.Empty);
        formula.AddIngredient(Guid.NewGuid(), 30m, 6m, NutritionalProfile.Empty);

        var shares = formula.Ingredients.Select(formula.ShareOf).ToList();

        Assert.Equal(1m, Math.Round(shares.Sum(), 10));
        Assert.Equal(Math.Round(2m / 3m, 10), Math.Round(shares[0], 10));
        Assert.Equal(22m, formula.TotalCostPerKgBdt); // 30 × 2/3 + 6 × 1/3
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(100.01, true)]
    [InlineData(99.98, false)]
    public void PercentagesSumTo100_AllowsSmallTolerance(double total, bool ok) =>
        Assert.Equal(ok, FeedFormula.PercentagesSumTo100([(decimal)total]));

    [Theory]
    [InlineData(true, 10, GroupPlanQuantityBasis.PerHead, 10)]
    [InlineData(true, 10, GroupPlanQuantityBasis.WholeGroup, 1)]
    [InlineData(false, 10, GroupPlanQuantityBasis.PerHead, 1)]
    [InlineData(true, 0, GroupPlanQuantityBasis.PerHead, 1)]
    public void HeadMultiplier_RespectsGroupQuantityBasis(bool group, int heads, GroupPlanQuantityBasis basis, int expected) =>
        Assert.Equal(expected, GroupFeedQuantity.HeadMultiplier(group, heads, basis));

    [Fact]
    public void WeightedAverageCost_KeepsSixDecimals()
    {
        var item = new InventoryItem(Guid.NewGuid(), _tenantId, Guid.NewGuid(), "Premix", "PMX", InventoryCategory.Feed, "g", 0m,
            initialStock: 1000m, initialCostBdt: 0.0021m);
        item.ReceiveStock(2000m, 0.0024m, Guid.NewGuid());

        Assert.Equal(0.0023m, item.WeightedAverageCostBdt); // (2.1 + 4.8) / 3000
        item.ReceiveStock(1m, 0.0025m, Guid.NewGuid());
        Assert.Equal(Math.Round((6.9m + 0.0025m) / 3001m, 6), item.WeightedAverageCostBdt);
    }
}
