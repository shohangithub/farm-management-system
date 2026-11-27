using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Intelligence;

public class SellOrKeepCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    /// <summary>
    /// A case that can be worked out by hand: gain starts at 1 kg/day and falls by 0.5 % of that per
    /// day (50 % over 100 days), feed does not scale with weight, price 400/kg, cost 301/day.
    /// One more day pays while 400 × (1 − 0.005 d) ≥ 301, i.e. up to day 49.
    /// </summary>
    private static readonly SellOrKeepAssumptions HandCase = new()
    {
        GrowthDeclineFraction = 0.5m,
        GrowthDeclineDays = 100,
        FeedScalingExponent = 0,
    };

    private static SellOrKeepInput Input(
        decimal pricePerKg = 400m,
        decimal feedPerDay = 301m,
        decimal invested = 60_000m,
        int treatments = 0,
        decimal? breedTarget = null,
        params SellOrKeepWeighing[] weighings) =>
        new()
        {
            AnimalId = Guid.NewGuid(),
            TagId = "T-01",
            BreedName = "Sahiwal Cross",
            AcquisitionDate = Today.AddDays(-100),
            Today = Today,
            Weighings = weighings.Length > 0
                ? weighings
                : [new(Today.AddDays(-40), 160m), new(Today.AddDays(-20), 180m), new(Today, 200m)],
            Cost = new AnimalCostBreakdown(invested, 0m, 0m, 0m, 0m),
            FeedCostInWindowBdt = feedPerDay * 30m,
            TreatmentsInWindow = treatments,
            BreedTargetAdgKg = breedTarget,
            Price = new SellOrKeepPrice(pricePerKg, SellOrKeepPriceSource.OwnSales, 3),
        };

    [Fact]
    public void Least_squares_slope_fits_scattered_weighings()
    {
        var (slope, r2) = SellOrKeepCalculator.LeastSquaresSlope(
        [
            new(Today.AddDays(-30), 100m),
            new(Today.AddDays(-20), 108m),
            new(Today.AddDays(-10), 118m),
            new(Today, 124m),
        ]);

        // Σ(x−x̄)(y−ȳ) = 410, Σ(x−x̄)² = 500.
        slope.Should().Be(0.82m);
        r2.Should().BeGreaterThan(0.98m).And.BeLessThan(1m);
    }

    [Fact]
    public void Slope_needs_two_weighings_on_different_days()
    {
        SellOrKeepCalculator.LeastSquaresSlope([new(Today, 100m)]).SlopeKgPerDay.Should().BeNull();
        SellOrKeepCalculator.LeastSquaresSlope([new(Today, 100m), new(Today, 102m)]).SlopeKgPerDay.Should().BeNull();
    }

    [Fact]
    public void Growth_falls_back_to_lifetime_gain_without_two_recent_weighings()
    {
        var advice = SellOrKeepCalculator.Advise(Input(weighings:
        [
            new(Today.AddDays(-100), 100m),
            new(Today.AddDays(-20), 180m),
        ]));

        advice.GrowthSource.Should().Be(SellOrKeepGrowthSource.Lifetime);
        advice.AdgKgPerDay.Should().Be(1m);
        advice.WeightTodayKg.Should().Be(200m, "the 20 days since the last weighing add 20 kg");
        advice.Confidence.Should().Be(SellOrKeepConfidence.Low);
    }

    [Fact]
    public void Price_source_priority_is_own_sales_then_breed_then_live_rate_then_meat()
    {
        SellOrKeepCalculator.ResolvePrice(425m, 2, 450m, true, 400m, 680m, 0.55m)
            .Should().Be(new SellOrKeepPrice(425m, SellOrKeepPriceSource.OwnSales, 2));
        SellOrKeepCalculator.ResolvePrice(425m, 1, 450m, true, 400m, 680m, 0.55m)
            .Should().Be(new SellOrKeepPrice(450m, SellOrKeepPriceSource.BreedExpected));
        SellOrKeepCalculator.ResolvePrice(null, 0, null, true, 400m, 680m, 0.55m)
            .Should().Be(new SellOrKeepPrice(400m, SellOrKeepPriceSource.FarmLiveRate));
        SellOrKeepCalculator.ResolvePrice(null, 0, null, false, 400m, 680m, 0.55m)
            .Should().Be(new SellOrKeepPrice(374m, SellOrKeepPriceSource.MeatAndDressing));
    }

    [Fact]
    public void Best_day_matches_the_hand_worked_case()
    {
        var advice = SellOrKeepCalculator.Advise(Input(), HandCase);

        advice.AdgKgPerDay.Should().Be(1m);
        advice.FeedPerDayBdt.Should().Be(301m);
        advice.ValueTodayBdt.Should().Be(80_000m);
        advice.ProfitTodayBdt.Should().Be(20_000m);

        // weight(49) = 200 + 49 − 0.005 × 49 × 50 / 2 = 242.875 kg → 97,150; cost = 60,000 + 49 × 301 = 74,749.
        advice.BestDay.Should().Be(49);
        advice.BestDate.Should().Be(Today.AddDays(49));
        advice.ProfitAtBestDayBdt.Should().Be(22_401m);
        advice.ExtraProfitBdt.Should().Be(2_401m);
        advice.BestWeightKg.Should().Be(242.9m);
        advice.MarginPerDayKeptBdt.Should().Be(97m, "day 1: 400 × 0.995 − 301");
        advice.BreakEvenDay.Should().BeNull("it is already in profit");

        advice.Verdict.Should().Be(SellOrKeepVerdict.Keep);
        advice.Confidence.Should().Be(SellOrKeepConfidence.Medium, "three recent weighings on a straight line");
        advice.ReasonEn.Should().StartWith("Keep about 49 more days: sell around 15 Jan 2027 at ~243 kg, expected profit ৳22,401 (৳2,401 more than today).");
        advice.ReasonBn.Should().StartWith("আরও প্রায় 49 দিন রাখুন");
        advice.Projection.Should().HaveCount(HandCase.HorizonDays + 1);
    }

    [Fact]
    public void Break_even_day_is_found_when_it_starts_at_a_loss()
    {
        var advice = SellOrKeepCalculator.Advise(Input(invested: 81_000m), HandCase);

        advice.ProfitTodayBdt.Should().Be(-1_000m);
        // Day d adds 400 × (1 − 0.005 d) − 301 = 99 − 2d: 957 after 11 days, 1,032 after 12.
        advice.BreakEvenDay.Should().Be(12);
        advice.Verdict.Should().Be(SellOrKeepVerdict.Keep);
    }

    [Fact]
    public void Loss_case_names_slow_growth_against_the_breed_target()
    {
        var advice = SellOrKeepCalculator.Advise(Input(pricePerKg: 300m, feedPerDay: 250m, invested: 70_000m, breedTarget: 1.0m, weighings:
        [
            new(Today.AddDays(-40), 180m),
            new(Today.AddDays(-20), 190m),
            new(Today, 200m),
        ]));

        advice.AdgKgPerDay.Should().Be(0.5m);
        advice.GrowthVsTarget.Should().Be(SellOrKeepGrowthVsTarget.Below);
        advice.ProfitTodayBdt.Should().Be(-10_000m);
        advice.BestDay.Should().Be(0);
        advice.Verdict.Should().Be(SellOrKeepVerdict.LosingMoney);
        advice.LossCause.Should().Be(SellOrKeepLossCause.SlowGrowth);
        advice.ReasonEn.Should().StartWith("Losing money: even at its best it stays ৳10,000 short of its cost — check feed and health: it gains 0.50 kg/day against a breed target of 1.00");
    }

    [Fact]
    public void Loss_case_without_a_breed_target_blames_feed_cost_per_kg_gained()
    {
        var advice = SellOrKeepCalculator.Advise(Input(pricePerKg: 300m, feedPerDay: 250m, invested: 70_000m, weighings:
        [
            new(Today.AddDays(-40), 180m),
            new(Today.AddDays(-20), 190m),
            new(Today, 200m),
        ]));

        advice.FeedCostPerKgGainBdt.Should().Be(500m);
        advice.LossCause.Should().Be(SellOrKeepLossCause.FeedCost);
    }

    [Fact]
    public void Not_enough_weighings_asks_to_weigh_first()
    {
        var advice = SellOrKeepCalculator.Advise(Input(weighings: [new(Today.AddDays(-5), 200m)]), HandCase);

        advice.Verdict.Should().Be(SellOrKeepVerdict.WeighFirst);
        advice.Confidence.Should().Be(SellOrKeepConfidence.Low);
        advice.AdgKgPerDay.Should().BeNull();
        advice.BestDay.Should().BeNull();
        advice.ProfitTodayBdt.Should().Be(20_000m, "today's value is still known from the one weighing");
        advice.ReasonEn.Should().StartWith("Weigh it first");
    }

    [Fact]
    public void Often_sick_animal_in_profit_is_sold_now_with_lower_confidence()
    {
        var advice = SellOrKeepCalculator.Advise(Input(treatments: 3), HandCase);

        advice.FrequentlySick.Should().BeTrue();
        advice.BestDay.Should().Be(49, "the maths alone would keep it");
        advice.Verdict.Should().Be(SellOrKeepVerdict.SellNow);
        advice.Confidence.Should().Be(SellOrKeepConfidence.Low);
        advice.ReasonEn.Should().StartWith("Sell now: it is often sick (3 treatments in 90 days");
    }

    [Fact]
    public void High_health_cost_against_feed_also_counts_as_often_sick()
    {
        var input = Input() with { HealthCostInWindowBdt = 90m * 100m }; // 100/day against 301/day feed
        SellOrKeepCalculator.Advise(input, HandCase).FrequentlySick.Should().BeTrue();
    }

    [Fact]
    public void Gain_worth_less_than_a_days_cost_means_sell_now()
    {
        // 0.5 kg/day × 400 = 200 a day against 301 a day of cost, but still in profit today.
        var advice = SellOrKeepCalculator.Advise(Input(weighings:
        [
            new(Today.AddDays(-40), 180m),
            new(Today.AddDays(-20), 190m),
            new(Today, 200m),
        ]));

        advice.MarginPerDayKeptBdt.Should().BeNegative();
        advice.BestDay.Should().Be(0);
        advice.Verdict.Should().Be(SellOrKeepVerdict.SellNow);
    }
}

public class AnimalSellOrKeepAdvisorTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    [Fact]
    public async Task Prices_with_the_farms_own_sales_of_the_same_breed()
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var sahiwal = new Breed(Guid.NewGuid(), tenant, "Sahiwal Cross", "", "Crossbred", "", "Beef",
            0.5m, 0.7m, 0.9m, 1.1m, 6m, 8m, 0.8m, 1.2m, 0m, 0m, 0m, 0m, "");
        sahiwal.SetExpectedPricePerKg(450m);

        Animal Make(string tag, Guid breedId) => Animal.Create(tenant, farm, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            breedId, AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, Today.AddDays(-120), 60_000m, null);

        var onFarm = Make("T-01", sahiwal.Id);
        var sold1 = Make("S-01", sahiwal.Id);
        sold1.Sell(80_000m, Today.AddDays(-60), Guid.NewGuid(), "Karim", 200m);
        var sold2 = Make("S-02", sahiwal.Id);
        sold2.Sell(90_000m, Today.AddDays(-30), Guid.NewGuid(), "Rahim", 200m);

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetAllByFarmAsync(farm, Arg.Any<CancellationToken>()).Returns([onFarm, sold1, sold2]);
        animals.GetWeighInsByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([(onFarm.Id, Today.AddDays(-40), 160m), (onFarm.Id, Today.AddDays(-20), 180m), (onFarm.Id, Today, 200m)]);
        var breeds = Substitute.For<IBreedRepository>();
        breeds.GetAllAsync(Arg.Any<CancellationToken>()).Returns([sahiwal]);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByFarmIdAsync(farm, Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalCostLedger>());
        var feed = Substitute.For<IAnimalFeedAllocationRepository>();
        feed.GetTotalsForFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new AnimalFeedTotals(onFarm.Id, 500m, 9_000m, 30)]);
        feed.GetDailyEatenByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new AnimalFeedDay(onFarm.Id, Today.AddDays(-1), 15m, 9_000m)]);
        var overheads = Substitute.For<IAnimalOverheadAllocationRepository>();
        overheads.GetByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AnimalOverheadAllocation>());
        var treatments = Substitute.For<IMedicalTreatmentRepository>();
        treatments.GetByFarmStartedBetweenAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MedicalTreatment>());
        var vaccinations = Substitute.For<IVaccinationRepository>();
        vaccinations.GetAdministeredByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(VaccinationEvent, string)>());
        var valuation = new AnimalValuationService(new FixedRules());

        var advisor = new AnimalSellOrKeepAdvisor(animals, breeds, Substitute.For<IAnimalBatchRepository>(), ledgers, feed, overheads,
            treatments, vaccinations, Substitute.For<IInventoryItemRepository>(), valuation);

        var advice = await advisor.AdviseFarmAsync(farm, Today, includeProjection: true);

        advice.Should().ContainSingle();
        var x = advice[0];
        x.TagId.Should().Be("T-01");
        x.Price.Should().Be(new SellOrKeepPrice(425m, SellOrKeepPriceSource.OwnSales, 2), "(80,000 + 90,000) ÷ (200 + 200) kg; own sales beat the breed's expected price");
        x.Cost.TotalBdt.Should().Be(69_000m, "purchase + allocated feed when there is no cost book yet");
        x.FeedPerDayBdt.Should().Be(300m);
        x.AdgKgPerDay.Should().Be(1m);
        x.BreedTargetAdgKg.Should().Be(1.0m, "the middle of the breed's standard 0.8–1.2 kg/day range");
        x.Projection.Should().NotBeEmpty();
    }

    private sealed class FixedRules : Farm360.Application.Common.Interfaces.IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Current { get; } = new();
    }
}
