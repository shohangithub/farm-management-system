using System;
using System.Linq;
using Farm360.Domain.Intelligence.Projections;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Intelligence;

public class FatteningProjectionCalculatorTests
{
    private FatteningProjectionInputs GetSampleInputs()
    {
        return new FatteningProjectionInputs(
            StartingLiveWeightKg: 200m,
            PurchasePriceBdt: 80000m,
            CurrentMeatPriceBdtPerKg: 680m,
            InitialMeatYieldRatio: 0.50m,
            DailyLiveWeightGainKg: 0.70m,
            MeatYieldOnDailyGainRatio: 0.50m,
            DailyFeedQuantityKgAtStart: 3.0m,
            FeedPriceBdtPerKg: 44.30m,
            DailyGrassCostBdt: 20m,
            DailyOtherCostBdt: 30m,
            MonthlyLaborCostBdt: 1500m,
            FatteningPeriodDays: 120
        );
    }

    [Fact]
    public void Calculate_WithSampleInputs_MatchesGoldenVectors()
    {
        var inputs = GetSampleInputs();
        var result = FatteningProjectionCalculator.Calculate(inputs);

        // Day 1
        var day1 = result.Days.First(x => x.Day == 1);
        day1.LiveWeightKg.Should().BeApproximately(200.70m, 0.01m);
        day1.MeatWeightKg.Should().BeApproximately(100.35m, 0.01m);
        day1.FeedQtyKg.Should().BeApproximately(3.010m, 0.01m);
        day1.DailyTotalCostBdt.Should().BeApproximately(233.37m, 0.01m);
        day1.CumulativeCostBdt.Should().BeApproximately(233.37m, 0.01m);
        day1.TotalInvestmentBdt.Should().BeApproximately(80233.37m, 0.01m);
        day1.MeatValueBdt.Should().BeApproximately(68238.00m, 0.01m);
        day1.ProfitLossBdt.Should().BeApproximately(-11995.37m, 0.01m);

        // Day 120
        var day120 = result.Days.First(x => x.Day == 120);
        day120.LiveWeightKg.Should().BeApproximately(284.00m, 0.01m);
        day120.MeatWeightKg.Should().BeApproximately(142.00m, 0.01m);
        day120.FeedQtyKg.Should().BeApproximately(4.260m, 0.01m);
        day120.DailyTotalCostBdt.Should().BeApproximately(288.72m, 0.01m);
        day120.CumulativeCostBdt.Should().BeApproximately(31324.99m, 0.01m);
        day120.TotalInvestmentBdt.Should().BeApproximately(111324.99m, 0.01m);
        day120.MeatValueBdt.Should().BeApproximately(96560.00m, 0.01m);
        day120.ProfitLossBdt.Should().BeApproximately(-14764.99m, 0.01m);
        
        // Summary
        result.Summary.TotalFarmingCostBdt.Should().BeApproximately(31324.99m, 0.01m);
        result.Summary.TotalInvestmentBdt.Should().BeApproximately(111324.99m, 0.01m);
        result.Summary.BreakEvenPricePerLiveKgBdt.Should().BeApproximately(391.99m, 0.01m);
        result.Summary.BreakEvenPricePerMeatKgBdt.Should().BeApproximately(783.98m, 0.01m);
        result.Summary.OptimalSaleDay.Should().Be(10);
        result.Summary.OptimalProfitBdt.Should().BeApproximately(-11974.58m, 0.01m);
    }
}
