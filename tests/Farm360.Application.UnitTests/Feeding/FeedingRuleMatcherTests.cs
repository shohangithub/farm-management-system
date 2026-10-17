using System;
using System.Linq;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>Which rule line feeds an animal — the ranges as the rule-set form writes them (81–150, 151–180…).</summary>
public class FeedingRuleMatcherTests
{
    private static FeedingRuleSet WeightSet(FeedingPlanType type = FeedingPlanType.WeightQuantity)
    {
        var set = new FeedingRuleSet(Guid.NewGuid(), Guid.NewGuid(), "দানাদার", TargetAnimalType.Cattle, FeedingPurpose.Growth, type);
        // Entered out of order, as in the real rule set.
        set.AddRuleLine(351m, 400m, null, null, FeedCategory.Concentrate, 4m);
        set.AddRuleLine(81m, 150m, null, null, FeedCategory.Concentrate, 1.5m);
        set.AddRuleLine(401m, 450m, null, null, FeedCategory.Concentrate, 4.5m);
        set.AddRuleLine(151m, 180m, null, null, FeedCategory.Concentrate, 2m);
        set.AddRuleLine(181m, 300m, null, null, FeedCategory.Concentrate, 2.5m);
        set.AddRuleLine(301m, 350m, null, null, FeedCategory.Concentrate, 3.5m);
        return set;
    }

    [Theory]
    [InlineData(81, 1.5)]
    [InlineData(150, 1.5)]      // top of a range is inside it
    [InlineData(150.5, 1.5)]    // between 150 and 151: still the 81–150 ration until 151 is reached
    [InlineData(151, 2)]
    [InlineData(180, 2)]
    [InlineData(180.5, 2)]      // used to fall back to the LOWEST range (1.5 kg)
    [InlineData(181, 2.5)]
    [InlineData(400.9, 4)]
    [InlineData(450, 4.5)]
    public void WeightRanges_AreInclusive_AndCoverTheGapsBetweenThem(double weight, double expectedKg)
    {
        var set = WeightSet();

        var match = FeedingRuleMatcher.Match(set, (decimal)weight, null);

        match.Lines.Should().ContainSingle();
        FeedingRuleMatcher.ExpectedKg(set, match.Primary!, (decimal)weight).Should().Be((decimal)expectedKg);
        match.Fit.Should().Be(RuleMatchFit.InRange);
    }

    [Fact]
    public void BelowTheLowestRange_UsesTheLowestRation_AndSaysSo()
    {
        var match = FeedingRuleMatcher.Match(WeightSet(), 60m, null);

        match.Primary!.ConcentrateKgPerDay.Should().Be(1.5m);
        match.Fit.Should().Be(RuleMatchFit.BelowRange);
    }

    [Fact]
    public void AboveTheHighestRange_UsesTheHighestRation_AndSaysSo()
    {
        var match = FeedingRuleMatcher.Match(WeightSet(), 520m, null);

        match.Primary!.ConcentrateKgPerDay.Should().Be(4.5m);
        match.Fit.Should().Be(RuleMatchFit.AboveRange);
    }

    [Fact]
    public void UnknownWeight_UsesTheLowestRation_AndSaysSo()
    {
        var match = FeedingRuleMatcher.Match(WeightSet(), 0m, null);

        match.Primary!.ConcentrateKgPerDay.Should().Be(1.5m);
        match.Fit.Should().Be(RuleMatchFit.Unknown);
    }

    [Fact]
    public void PercentageOfBodyWeight_IsAppliedToTheAnimalsWeight()
    {
        var set = new FeedingRuleSet(Guid.NewGuid(), Guid.NewGuid(), "3%", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.WeightPercentage);
        set.AddRuleLine(0m, 300m, null, null, FeedCategory.Concentrate, 2.5m);
        set.AddRuleLine(301m, null, null, null, FeedCategory.Concentrate, 3m);

        var match = FeedingRuleMatcher.Match(set, 400m, null);

        FeedingRuleMatcher.ExpectedKg(set, match.Primary!, 400m).Should().Be(12m);
    }

    [Fact]
    public void AgePlans_PickOneRangeByAge_NotEveryRange()
    {
        var set = new FeedingRuleSet(Guid.NewGuid(), Guid.NewGuid(), "Calf", TargetAnimalType.Cattle, FeedingPurpose.Starter, FeedingPlanType.AgeBased);
        set.AddRuleLine(null, null, 0, 60, FeedCategory.Concentrate, 1.5m);
        set.AddRuleLine(null, null, 61, 180, FeedCategory.Concentrate, 3.5m);
        set.AddRuleLine(null, null, 181, null, FeedCategory.Concentrate, 5m);

        FeedingRuleMatcher.Match(set, 0m, 30).Lines.Should().ContainSingle().Which.ConcentrateKgPerDay.Should().Be(1.5m);
        FeedingRuleMatcher.Match(set, 0m, 60).Primary!.ConcentrateKgPerDay.Should().Be(1.5m);
        FeedingRuleMatcher.Match(set, 0m, 100).Primary!.ConcentrateKgPerDay.Should().Be(3.5m);
        FeedingRuleMatcher.Match(set, 0m, 900).Primary!.ConcentrateKgPerDay.Should().Be(5m);
    }

    [Fact]
    public void AgeFromDateOfBirth_IsWholeDays_AndNullWhenUnusable()
    {
        FeedingRuleMatcher.AgeInDays(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 2)).Should().Be(60);
        FeedingRuleMatcher.AgeInDays(new DateOnly(2027, 1, 1), new DateOnly(2026, 3, 2)).Should().BeNull();
        FeedingRuleMatcher.AgeInDays(default, new DateOnly(2026, 3, 2)).Should().BeNull();
    }

    [Fact]
    public void LinesSharingARange_AreAllFed()
    {
        var set = new FeedingRuleSet(Guid.NewGuid(), Guid.NewGuid(), "Mix", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.WeightQuantity);
        set.AddRuleLine(100m, 200m, null, null, FeedCategory.Concentrate, 2m);
        set.AddRuleLine(100m, 200m, null, null, FeedCategory.Forage, 6m);
        set.AddRuleLine(201m, 300m, null, null, FeedCategory.Concentrate, 3m);

        FeedingRuleMatcher.Match(set, 150m, null).Lines.Select(l => l.FeedType)
            .Should().BeEquivalentTo([FeedCategory.Concentrate, FeedCategory.Forage]);
    }

    [Fact]
    public void FixedQuantity_FeedsEveryLine_WhateverTheWeight()
    {
        var set = new FeedingRuleSet(Guid.NewGuid(), Guid.NewGuid(), "Mineral", TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
        set.AddRuleLine(null, null, null, null, FeedCategory.Mineral, 0.1m);

        var match = FeedingRuleMatcher.Match(set, 0m, null);

        match.Lines.Should().ContainSingle();
        match.Fit.Should().Be(RuleMatchFit.InRange);
    }
}
