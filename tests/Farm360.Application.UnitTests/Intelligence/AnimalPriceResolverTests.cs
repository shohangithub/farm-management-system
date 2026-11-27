using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.BusinessRules;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Intelligence.Pricing;
using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding;
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

public class AnimalPriceResolverTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);

    private static FarmBusinessRules Rules(
        AnimalValuationPolicy valuation = AnimalValuationPolicy.MeatYieldMarket,
        decimal meat = 680m,
        decimal dressing = 0.55m,
        decimal live = 420m,
        decimal byProductsPercent = 0m,
        decimal byProductsPerHead = 0m) => new()
    {
        Valuation = valuation,
        DefaultMeatPricePerKgBdt = meat,
        DefaultDressingPercentage = dressing,
        DefaultLiveWeightPricePerKgBdt = live,
        ByProductsPercentOfMeat = byProductsPercent,
        ByProductsValuePerHeadBdt = byProductsPerHead,
    };

    // ── Priority ────────────────────────────────────────────────────────────

    [Fact]
    public void Priority_is_own_sales_then_breed_price_then_the_farm_rule()
    {
        var rules = Rules(AnimalValuationPolicy.LiveWeightMarket);

        var own = AnimalPriceResolution.Resolve(425m, 3, 450m, "Deshi (Local)", rules);
        own.Source.Should().Be(AnimalPriceSource.OwnSales);
        own.PricePerKgBdt.Should().Be(425m);
        own.SalesCount.Should().Be(3);
        own.ExplanationEn.Should().Be("your own sales of 3 Deshi (Local) in the last 12 months, average ৳425/kg");

        var breed = AnimalPriceResolution.Resolve(425m, 1, 450m, "Deshi (Local)", rules);
        breed.Source.Should().Be(AnimalPriceSource.BreedExpected, "one own sale is not enough");
        breed.PricePerKgBdt.Should().Be(450m);
        breed.ExplanationEn.Should().Be("breed price ৳450/kg");

        var live = AnimalPriceResolution.Resolve(null, 0, null, "Deshi (Local)", rules);
        live.Source.Should().Be(AnimalPriceSource.FarmLiveRate);
        live.PricePerKgBdt.Should().Be(420m);
        live.ExplanationEn.Should().Be("live-weight price ৳420/kg (farm rule)");

        var meat = AnimalPriceResolution.Resolve(null, 0, null, "Deshi (Local)", Rules());
        meat.Source.Should().Be(AnimalPriceSource.MeatAndDressing);
        meat.PricePerKgBdt.Should().Be(374m, "680 × 55 %");
        meat.ExplanationEn.Should().Be("meat ৳680 × dressing 55%");
    }

    [Fact]
    public void Own_sales_and_breed_price_carry_no_per_head_addition()
    {
        var rules = Rules(byProductsPercent: 0.12m, byProductsPerHead: 3_000m);

        AnimalPriceResolution.Resolve(425m, 2, null, "X", rules).PerHeadBdt.Should().Be(0m, "a live price already includes the by-products");
        AnimalPriceResolution.Resolve(null, 0, 450m, "X", rules).PerHeadBdt.Should().Be(0m);
        AnimalPriceResolution.Resolve(null, 0, null, "X", Rules(AnimalValuationPolicy.LiveWeightMarket, byProductsPercent: 0.12m, byProductsPerHead: 3_000m))
            .Should().Match<AnimalPrice>(p => p.PerHeadBdt == 0m && p.PricePerKgBdt == 420m);
    }

    // ── By-product maths ────────────────────────────────────────────────────

    [Fact]
    public void Meat_yield_adds_by_products_percent_per_kg_and_a_fixed_amount_per_head()
    {
        var price = AnimalPriceResolution.FarmRule(Rules(byProductsPercent: 0.12m, byProductsPerHead: 3_000m));

        price.PricePerKgBdt.Should().Be(418.88m, "680 × 0.55 × 1.12");
        price.PerHeadBdt.Should().Be(3_000m);
        price.MarketValueAt(150m).Should().Be(65_832m, "150 × 418.88 = 62,832, + 3,000 per head");
        price.ExplanationEn.Should().Be("meat ৳680 × dressing 55% + by-products 12% + ৳3,000 per head");
        price.ExplanationBn.Should().Contain("উপজাত 12%").And.Contain("৳3,000");
    }

    [Fact]
    public void By_products_default_to_zero_so_existing_values_do_not_change()
    {
        var defaults = new FarmBusinessRules();
        defaults.ByProductsPercentOfMeat.Should().Be(0m);
        defaults.ByProductsValuePerHeadBdt.Should().Be(0m);

        var price = AnimalPriceResolution.FarmRule(Rules());
        price.MarketValueAt(150m).Should().Be(56_100m, "150 × 374");
    }

    [Fact]
    public void Valuation_service_uses_the_same_by_product_maths()
    {
        var provider = new TestRulesProvider();
        provider.Rules.DefaultMeatPricePerKgBdt = 680m;
        provider.Rules.DefaultDressingPercentage = 0.55m;
        provider.Rules.ByProductsPercentOfMeat = 0.12m;
        provider.Rules.ByProductsValuePerHeadBdt = 3_000m;
        var valuation = new AnimalValuationService(provider);

        valuation.LiveWeightRatePerKgBdt.Should().Be(418.88m);
        valuation.ValueAtWeight(150m, 1m).Should().Be(65_832m);
        valuation.ValueAtWeight(150m, 1m, ratePerKgOverride: 400m).Should().Be(60_000m, "a typed live rate replaces the price and the per-head addition");
    }

    [Fact]
    public void A_typed_rate_replaces_the_price_and_drops_the_per_head_addition()
    {
        var price = AnimalPriceResolution.FarmRule(Rules(byProductsPercent: 0.12m, byProductsPerHead: 3_000m));
        price.ValueAt(150m, 50_000m, ratePerKgOverride: 430m).Should().Be(64_500m);
    }

    // ── Cost basis ──────────────────────────────────────────────────────────

    [Fact]
    public void Cost_basis_has_no_market_value_and_values_at_cost()
    {
        var price = AnimalPriceResolution.Resolve(null, 0, null, "Deshi", Rules(AnimalValuationPolicy.CostBasis, byProductsPerHead: 3_000m));

        price.Source.Should().Be(AnimalPriceSource.CostBasis);
        price.ValuedAtCost.Should().BeTrue();
        price.ValueAt(150m, 72_500m).Should().Be(72_500m, "valued at cost so far");
        price.ExplanationEn.Should().StartWith("valued at cost (farm rule)");
        price.PricePerKgBdt.Should().Be(374m, "the meat estimate is still carried for forward-looking tools");
        price.ValueAt(150m, 72_500m, ratePerKgOverride: 400m).Should().Be(60_000m, "a what-if rate still works");
    }

    [Fact]
    public void Own_sales_and_breed_price_still_win_over_cost_basis()
    {
        var rules = Rules(AnimalValuationPolicy.CostBasis);
        AnimalPriceResolution.Resolve(425m, 2, null, "X", rules).ValuedAtCost.Should().BeFalse();
        AnimalPriceResolution.Resolve(null, 0, 450m, "X", rules).ValuedAtCost.Should().BeFalse();
    }

    // ── The resolver service ────────────────────────────────────────────────

    [Fact]
    public async Task Resolver_averages_own_sales_of_the_breed_in_the_last_12_months_on_this_farm_only()
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var breed = MakeBreed(tenant, "Deshi (Local)", expected: 410m);

        var onFarm = Make(tenant, farm, "A-1", breed.Id);
        var s1 = Make(tenant, farm, "S-1", breed.Id);
        s1.Sell(84_000m, Today.AddDays(-40), Guid.NewGuid(), null, 200m);
        var s2 = Make(tenant, farm, "S-2", breed.Id);
        s2.Sell(86_000m, Today.AddDays(-100), Guid.NewGuid(), null, 200m);
        var old = Make(tenant, farm, "S-3", breed.Id, Today.AddDays(-500));
        old.Sell(10_000m, Today.AddDays(-400), Guid.NewGuid(), null, 200m);

        var resolver = TestPrices.Resolver(new TestRulesProvider(), [onFarm, s1, s2, old], [breed], farm);
        var price = await resolver.ForAnimalAsync(onFarm, Today);

        price.Source.Should().Be(AnimalPriceSource.OwnSales);
        price.PricePerKgBdt.Should().Be(425m, "(84,000 + 86,000) ÷ 400 kg; the sale 400 days ago is outside the 12 months");
        price.SalesCount.Should().Be(2);
    }

    [Fact]
    public void Resolver_uses_the_farms_own_rule_overrides()
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var provider = new PerFarmRules(new FarmBusinessRules(), (tenant, farm, Rules(AnimalValuationPolicy.LiveWeightMarket, live: 440m)));
        var resolver = new AnimalPriceResolver(Substitute.For<IAnimalRepository>(), Substitute.For<IBreedRepository>(), provider);

        var book = resolver.FromLoaded(tenant, farm, [], new Dictionary<Guid, Breed>(), Today);

        book.For(Guid.NewGuid()).Should().Match<AnimalPrice>(p => p.Source == AnimalPriceSource.FarmLiveRate && p.PricePerKgBdt == 440m);
    }

    // ── The animal card and the advisor agree ───────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Animal_page_card_and_sell_or_keep_advisor_give_the_same_price(bool withOwnSales)
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var breed = MakeBreed(tenant, "Deshi (Local)", expected: null);
        var animal = Make(tenant, farm, "A-1", breed.Id);
        animal.RecordWeight(Weight.Create(140m), Today.AddDays(-20), Guid.NewGuid(), null);
        animal.RecordWeight(Weight.Create(150m), Today, Guid.NewGuid(), null);
        var herd = new List<Animal> { animal };
        if (withOwnSales)
        {
            var s1 = Make(tenant, farm, "S-1", breed.Id);
            s1.Sell(85_000m, Today.AddDays(-10), Guid.NewGuid(), null, 200m);
            var s2 = Make(tenant, farm, "S-2", breed.Id);
            s2.Sell(85_000m, Today.AddDays(-20), Guid.NewGuid(), null, 200m);
            herd.AddRange([s1, s2]);
        }

        var rules = new TestRulesProvider();
        rules.Rules.DefaultMeatPricePerKgBdt = 680m;
        rules.Rules.DefaultDressingPercentage = 0.55m;
        rules.Rules.ByProductsPercentOfMeat = 0.12m;
        rules.Rules.ByProductsValuePerHeadBdt = 3_000m;

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        animals.GetAllByFarmAsync(farm, Arg.Any<CancellationToken>()).Returns(herd);
        animals.GetWeighInsByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([(animal.Id, Today.AddDays(-20), 140m), (animal.Id, Today, 150m)]);
        var breeds = Substitute.For<IBreedRepository>();
        breeds.GetAllAsync(Arg.Any<CancellationToken>()).Returns([breed]);
        breeds.GetByIdAsync(breed.Id, Arg.Any<CancellationToken>()).Returns(breed);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByFarmIdAsync(farm, Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalCostLedger>());
        var feed = Substitute.For<IAnimalFeedAllocationRepository>();
        feed.GetTotalsForFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalFeedTotals>());
        feed.GetDailyEatenByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalFeedDay>());
        var plans = Substitute.For<IAnimalFeedingPlanRepository>();
        plans.GetActivePlansForAnimalAsync(Arg.Any<Guid>(), animal.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalFeedingPlan>());
        var overheads = Substitute.For<IAnimalOverheadAllocationRepository>();
        overheads.GetByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AnimalOverheadAllocation>());
        var treatments = Substitute.For<IMedicalTreatmentRepository>();
        treatments.GetByFarmStartedBetweenAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<MedicalTreatment>());
        var vaccinations = Substitute.For<IVaccinationRepository>();
        vaccinations.GetAdministeredByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(VaccinationEvent, string)>());

        var clock = FixedClock(Today);
        var valuation = new AnimalValuationService(rules);
        var prices = new AnimalPriceResolver(animals, breeds, rules, clock);

        // The animal page card reads its price from the projection defaults.
        var defaults = await new ProjectionDefaultsResolver(animals, breeds, ledgers, feed, plans, valuation, prices, clock)
            .ResolveDefaultsAsync(animal.Id, CancellationToken.None);
        var card = defaults.MarketPrice!;

        var advice = await new AnimalSellOrKeepAdvisor(animals, breeds, Substitute.For<IAnimalBatchRepository>(), ledgers, feed, overheads,
                treatments, vaccinations, Substitute.For<IInventoryItemRepository>(), valuation, prices, clock)
            .AdviseFarmAsync(farm, Today);

        var x = advice.Should().ContainSingle().Subject;
        card.PricePerKgBdt.Should().Be(x.Price.PricePerKgBdt);
        card.PerHeadBdt.Should().Be(x.Price.PerHeadBdt);
        card.Source.Should().Be(x.Price.Source.ToString());
        card.ExplanationEn.Should().Be(x.Price.ExplanationEn);
        defaults.LiveWeightValuePerKgBdt!.Value.Should().Be(x.Price.PricePerKgBdt);

        if (withOwnSales)
        {
            card.PricePerKgBdt.Should().Be(425m);
            card.PerHeadBdt.Should().Be(0m);
        }
        else
        {
            card.PricePerKgBdt.Should().Be(418.88m);
            card.PerHeadBdt.Should().Be(3_000m);
            x.ValueTodayBdt.Should().Be(65_832m, "150 kg × 418.88 + 3,000 — the card's value at the same weight");
        }
    }

    // ── Validation ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0, true)]
    [InlineData(0.5, true)]
    [InlineData(0.51, false)]
    public void Validator_keeps_by_products_percent_between_0_and_50(double pct, bool ok)
    {
        var result = new SaveBusinessRulesSettingsCommandValidator()
            .Validate(new SaveBusinessRulesSettingsCommand(null, new BusinessRulesOverrides(ByProductsPercentOfMeat: (decimal)pct)));
        result.IsValid.Should().Be(ok);
    }

    [Fact]
    public void Settings_row_round_trips_and_layers_the_by_product_overrides()
    {
        var row = TenantBusinessSettings.Create(Guid.NewGuid(), Guid.NewGuid(),
            new BusinessRulesOverrides(ByProductsValuePerHeadBdt: 3_000m, ByProductsPercentOfMeat: 0.12m));

        var overrides = row.ToOverrides();
        overrides.IsEmpty.Should().BeFalse();
        var effective = FarmBusinessRulesResolution.Apply(new FarmBusinessRules(), overrides);
        effective.ByProductsValuePerHeadBdt.Should().Be(3_000m);
        effective.ByProductsPercentOfMeat.Should().Be(0.12m);

        var bad = () => row.Update(new BusinessRulesOverrides(ByProductsPercentOfMeat: 0.6m));
        bad.Should().Throw<ArgumentOutOfRangeException>();
        var negative = () => row.Update(new BusinessRulesOverrides(ByProductsValuePerHeadBdt: -1m));
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Breed MakeBreed(Guid tenant, string name, decimal? expected)
    {
        var b = new Breed(Guid.NewGuid(), tenant, name, "", "Native", "", "Beef",
            0.3m, 0.4m, 0.5m, 0.6m, 0.3m, 0.5m, 8m, 10m, 0m, 0m, 0m, 0m, "");
        b.SetExpectedPricePerKg(expected);
        return b;
    }

    private static Animal Make(Guid tenant, Guid farm, string tag, Guid breedId, DateOnly? acquired = null) =>
        Animal.Create(tenant, farm, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            breedId, AnimalSex.Male, new DateOnly(2024, 1, 1), AcquisitionType.Purchased, acquired ?? Today.AddDays(-120), 50_000m, null);

    private sealed class PerFarmRules(FarmBusinessRules current, (Guid Tenant, Guid Farm, FarmBusinessRules Rules) farm) : IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Current => current;

        public FarmBusinessRules For(Guid tenantId, Guid? farmId = null) =>
            tenantId == farm.Tenant && farmId == farm.Farm ? farm.Rules : current;
    }

    private static IFarmClock FixedClock(DateOnly today)
    {
        var clock = Substitute.For<IFarmClock>();
        clock.Today.Returns(today);
        clock.UtcNow.Returns(today.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc));
        clock.Now.Returns(today.ToDateTime(new TimeOnly(12, 0)));
        clock.TimeZone.Returns(TimeZoneInfo.Utc);
        return clock;
    }
}
