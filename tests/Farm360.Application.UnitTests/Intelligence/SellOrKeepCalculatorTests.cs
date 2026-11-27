using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Pricing;
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
            Price = new AnimalPrice(pricePerKg, AnimalPriceSource.OwnSales, 3),
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
    public void By_products_per_head_add_to_value_and_profit_but_not_to_the_best_day()
    {
        var plain = SellOrKeepCalculator.Advise(Input(), HandCase);
        var withByProducts = SellOrKeepCalculator.Advise(Input() with { Price = new AnimalPrice(400m, AnimalPriceSource.MeatAndDressing, 0, 3_000m) }, HandCase);

        withByProducts.ValueTodayBdt.Should().Be(plain.ValueTodayBdt + 3_000m);
        withByProducts.ProfitTodayBdt.Should().Be(plain.ProfitTodayBdt + 3_000m);
        withByProducts.Projection[10].ValueBdt.Should().Be(plain.Projection[10].ValueBdt + 3_000m);
        withByProducts.BestDay.Should().Be(plain.BestDay, "a fixed amount per head does not change when to sell");
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

    // ── Fattening plan (one engine with the card) ──────────────────────────

    /// <summary>
    /// Like the owner's B-05: weighed 105.2 kg on arrival 120 days ago and 150 kg four days ago (0.386 kg/day
    /// since arrival), 0.55 kg/day over the last 60 days, ৳67,192 invested, feed ৳129/day + labour ৳65/day,
    /// priced ৳374/kg live (680 × 55 %) + ৳3,000 by-products per head.
    /// </summary>
    internal static SellOrKeepInput B05() => new()
    {
        AnimalId = Guid.NewGuid(),
        TagId = "B-05",
        BreedName = "Deshi (Local)",
        AcquisitionDate = Today.AddDays(-120),
        Today = Today,
        Weighings =
        [
            new(Today.AddDays(-120), 105.2m),
            new(Today.AddDays(-58), 120.3m),
            new(Today.AddDays(-31), 135.15m),
            new(Today.AddDays(-4), 150m),
        ],
        Cost = new AnimalCostBreakdown(67_192m, 0m, 0m, 0m, 0m),
        FeedCostInWindowBdt = 129m * 30m,
        LabourOverheadInWindowBdt = 65m * 90m,
        LabourOverheadDaysCovered = 90,
        BreedTargetAdgKg = 0.30m,
        BreedTargetBasis = SellOrKeepBreedTargetBasis.StandardRange,
        BreedTargetMinKg = 0.2m,
        BreedTargetMaxKg = 0.4m,
        Price = new AnimalPrice(374m, AnimalPriceSource.MeatAndDressing, 0, 3_000m),
    };

    [Fact]
    public void Plan_without_overrides_is_the_card_figure_for_figure()
    {
        var card = SellOrKeepCalculator.Advise(B05());
        var plan = SellOrKeepCalculator.Advise(B05(), null, true, new SellOrKeepOverrides());

        plan.Should().BeEquivalentTo(card, o => o.Excluding(x => x.AnimalId));
        plan.Projection[0].Should().Be(card.Projection[0]);
        card.WeightTodayKg.Should().Be(152.2m, "150 kg four days ago + 4 × 0.55");
        card.ProfitTodayBdt.Should().Be(-7_269m, "152.2 × 374 + 3,000 − 67,192");
        card.DailyCostBdt.Should().Be(194m);
        card.FeedCostBasis.Should().Be(SellOrKeepCostBasis.RecentRecords);
        card.LabourOverheadCostBasis.Should().Be(SellOrKeepCostBasis.RecentRecords);
        card.HealthCostBasis.Should().Be(SellOrKeepCostBasis.None);
    }

    [Fact]
    public void Adg_is_labelled_since_arrival_and_last_60_days_and_the_projection_uses_the_recent_one()
    {
        var x = SellOrKeepCalculator.Advise(B05());

        x.FirstWeighingDate.Should().Be(Today.AddDays(-120));
        x.LifetimeAdgKgPerDay.Should().Be(0.3862m, "(150 − 105.2) ÷ 116 days: the animal page header's figure");
        x.RecentAdgKgPerDay.Should().Be(0.55m);
        x.RecentGrowthWindowDays.Should().Be(60);
        x.GrowthSource.Should().Be(SellOrKeepGrowthSource.Recent);
        x.AdgKgPerDay.Should().Be(0.55m);
        x.ProjectionAdgKgPerDay.Should().Be(0.55m);
        x.AdgOverridden.Should().BeFalse();
        x.BreedTargetAdgKg.Should().Be(0.30m);
        x.BreedTargetBasis.Should().Be(SellOrKeepBreedTargetBasis.StandardRange);
        x.BreedTargetMinKg.Should().Be(0.2m);
        x.BreedTargetMaxKg.Should().Be(0.4m);
        x.GrowthVsTarget.Should().Be(SellOrKeepGrowthVsTarget.Above);
    }

    [Fact]
    public void Deshi_breed_target_is_the_middle_of_its_standard_range_in_kg_per_day()
    {
        var deshi = new Breed(Guid.NewGuid(), Guid.NewGuid(), "Deshi (Local)", "", "Indigenous", "Bangladesh", "Dual-purpose",
            0.2m, 0.3m, 0.4m, 0.5m, 8m, 10m, 0.2m, 0.4m, 1m, 3m, 4.5m, 5.5m, "");

        var (target, basis, min, max) = AnimalSellOrKeepAdvisor.BreedTargetDetail(deshi);

        target.Should().Be(0.30m);
        basis.Should().Be(SellOrKeepBreedTargetBasis.StandardRange);
        (min, max).Should().Be((0.2m, 0.4m));
    }

    [Fact]
    public void Cost_per_kg_gain_is_the_whole_daily_cost_over_the_gain()
    {
        var x = SellOrKeepCalculator.Advise(B05());

        x.CostPerKgGainBdt.Should().Be(353m, "194 ÷ 0.55 = 352.7");
        x.FeedCostPerKgGainBdt.Should().Be(235m, "129 ÷ 0.55 = 234.5");
    }

    [Fact]
    public void Overrides_change_the_projection_but_not_todays_weight()
    {
        var card = SellOrKeepCalculator.Advise(B05());
        var x = SellOrKeepCalculator.Advise(B05(), null, true, new SellOrKeepOverrides
        {
            AdgKgPerDay = 0.8m,
            FeedPerDayBdt = 100m,
            LabourOverheadPerDayBdt = 40m,
            HealthPerDayBdt = 10m,
            HorizonDays = 90,
            GrowthSlowdownPercent = 0m,
        });

        x.WeightTodayKg.Should().Be(card.WeightTodayKg, "today's weight always comes from the weighings");
        x.ProfitTodayBdt.Should().Be(card.ProfitTodayBdt, "the price was not changed");
        x.AdgKgPerDay.Should().Be(0.55m, "the measured gain is still reported");
        x.ProjectionAdgKgPerDay.Should().Be(0.8m);
        x.AdgOverridden.Should().BeTrue();
        x.DailyCostBdt.Should().Be(150m);
        x.FeedCostBasis.Should().Be(SellOrKeepCostBasis.Override);
        x.LabourOverheadCostBasis.Should().Be(SellOrKeepCostBasis.Override);
        x.HealthCostBasis.Should().Be(SellOrKeepCostBasis.Override);
        x.HorizonDays.Should().Be(90);
        x.Projection.Should().HaveCount(91);
        x.GrowthSlowdownPercent.Should().Be(0m);
        x.Projection[90].WeightKg.Should().Be(152.2m + 90 * 0.8m, "no slowdown: a straight 0.8 kg/day");
        x.CostPerKgGainBdt.Should().Be(188m, "150 ÷ 0.8 = 187.5");
    }

    [Fact]
    public void A_price_override_revalues_today_and_every_day()
    {
        var x = SellOrKeepCalculator.Advise(B05(), null, true, new SellOrKeepOverrides { PricePerKgBdt = 450m, PerHeadBdt = 0m });

        x.PriceOverridden.Should().BeTrue();
        x.Price.PricePerKgBdt.Should().Be(450m);
        x.Price.PerHeadBdt.Should().Be(0m);
        x.ProfitTodayBdt.Should().Be(Math.Round(152.2m * 450m, 0) - 67_192m);
        x.Projection[0].ProfitBdt.Should().Be(x.ProfitTodayBdt);
    }

    [Fact]
    public void Break_even_price_at_the_horizon_makes_the_last_day_break_even()
    {
        var x = SellOrKeepCalculator.Advise(B05(), null, true, new SellOrKeepOverrides { HorizonDays = 120 });
        var last = x.Projection[^1];

        x.WeightAtHorizonKg.Should().Be(last.WeightKg);
        x.CostAtHorizonBdt.Should().Be(last.CostBdt);
        x.BreakEvenPricePerKgAtHorizonBdt.Should().BeApproximately((last.CostBdt - 3_000m) / last.WeightKg, 0.5m,
            "(total cost − per-head addition) ÷ weight on the last day");

        var atBreakEven = SellOrKeepCalculator.Advise(B05(), null, true,
            new SellOrKeepOverrides { HorizonDays = 120, PricePerKgBdt = x.BreakEvenPricePerKgAtHorizonBdt });
        atBreakEven.Projection[^1].ProfitBdt.Should().BeInRange(-2m, 2m);
    }

    [Fact]
    public void Roi_at_best_day_is_profit_over_cost()
    {
        var x = SellOrKeepCalculator.Advise(Input());
        var best = x.Projection[x.BestDay!.Value];

        x.RoiAtBestDayPercent.Should().Be(Math.Round(best.ProfitBdt / best.CostBdt * 100m, 1));
    }
}

public class AnimalSellOrKeepAdvisorTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    [Fact]
    public async Task Prices_with_the_farms_own_sales_of_the_same_breed()
    {
        var (advisor, farm, _) = Build();

        var advice = await advisor.AdviseFarmAsync(farm, Today, includeProjection: true);

        advice.Should().ContainSingle();
        var x = advice[0];
        x.TagId.Should().Be("T-01");
        x.Price.PricePerKgBdt.Should().Be(425m, "(80,000 + 90,000) ÷ (200 + 200) kg; own sales beat the breed's expected price");
        x.Price.Source.Should().Be(AnimalPriceSource.OwnSales);
        x.Price.SalesCount.Should().Be(2);
        x.Price.ExplanationEn.Should().Be("your own sales of 2 Sahiwal Cross in the last 12 months, average ৳425/kg");
        x.Cost.TotalBdt.Should().Be(69_000m, "purchase + allocated feed when there is no cost book yet");
        x.FeedPerDayBdt.Should().Be(300m);
        x.AdgKgPerDay.Should().Be(1m);
        x.BreedTargetAdgKg.Should().Be(1.0m, "the middle of the breed's standard 0.8–1.2 kg/day range");
        x.BreedTargetBasis.Should().Be(SellOrKeepBreedTargetBasis.StandardRange);
        x.Projection.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Fattening_plan_day_0_and_best_day_equal_the_sell_or_keep_card()
    {
        var (advisor, _, animalId) = Build();

        var card = await advisor.AdviseAnimalAsync(animalId, Today);
        var plan = await advisor.PlanAnimalAsync(animalId, null, Today);

        card.Should().NotBeNull();
        plan.Should().NotBeNull();
        plan!.Plan.Should().BeEquivalentTo(card);
        plan.Plan.Projection[0].Should().Be(card!.Projection[0]);
        plan.Defaults.ProfitTodayBdt.Should().Be(card.ProfitTodayBdt);
        plan.Defaults.Projection.Should().BeEmpty();

        // Overrides change the plan, never the defaults it shows next to each lever.
        var whatIf = await advisor.PlanAnimalAsync(animalId, new SellOrKeepOverrides { PricePerKgBdt = 500m, HorizonDays = 60 }, Today);
        whatIf!.Defaults.Price.PricePerKgBdt.Should().Be(425m);
        whatIf.Defaults.HorizonDays.Should().Be(240);
        whatIf.Plan.Price.PricePerKgBdt.Should().Be(500m);
        whatIf.Plan.Projection.Should().HaveCount(61);
        whatIf.Plan.Projection[0].WeightKg.Should().Be(card.Projection[0].WeightKg);
    }

    private static (AnimalSellOrKeepAdvisor Advisor, Guid FarmId, Guid AnimalId) Build()
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
        var rules = new FixedRules();
        var valuation = new AnimalValuationService(rules);
        var prices = new AnimalPriceResolver(animals, breeds, rules);

        animals.GetByIdAsync(onFarm.Id, Arg.Any<CancellationToken>()).Returns(onFarm);

        var advisor = new AnimalSellOrKeepAdvisor(animals, breeds, Substitute.For<IAnimalBatchRepository>(), ledgers, feed, overheads,
            treatments, vaccinations, Substitute.For<IInventoryItemRepository>(), valuation, prices);
        return (advisor, farm, onFarm.Id);
    }

    private sealed class FixedRules : Farm360.Application.Common.Interfaces.IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Current { get; } = new();
    }
}
