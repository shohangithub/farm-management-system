using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;
using Farm360.Application.Intelligence.Pricing;
using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Intelligence.Services;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Intelligence;

public class AnimalFinancialSnapshotTests
{
    private static ProjectionDefaultValueDto<T> V<T>(T value) => new(value, ProjectionSourceCode.AnimalRecord, "test");

    [Fact]
    public async Task Snapshot_is_today_based_on_cost_to_date_and_valuation_rate()
    {
        var animal = TestAnimals.Create(50_000m, 300m);
        var ledger = AnimalCostLedger.Create(TestAnimals.TenantId, animal.Id, TestAnimals.FarmId, 50_000m);
        ledger.UpdateFeedCost(30_000m);

        var defaults = new ProjectionDefaultsDto(
            StartingLiveWeightKg: V(300m),
            PurchasePriceBdt: V(50_000m),
            CurrentMeatPriceBdtPerKg: V(680m),
            InitialMeatYieldRatio: V(0.5m),
            DailyLiveWeightGainKg: V(0.6m),
            MeatYieldOnDailyGainRatio: V(0.5m),
            DailyFeedQuantityKgAtStart: V(6m),
            FeedPriceBdtPerKg: V(50m),
            DailyGrassCostBdt: V(20m),
            DailyOtherCostBdt: V(30m),
            MonthlyLaborCostBdt: V(1_500m),
            FatteningPeriodDays: V(120),
            CostToDateBdt: V(80_000m),
            LiveWeightValuePerKgBdt: V(340m));

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByAnimalIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        var resolver = Substitute.For<IProjectionDefaultsResolver>();
        resolver.ResolveDefaultsAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(defaults);

        // The forward costs are the Sell-or-keep projection's (actual run-rate), not a separate model.
        var advice = SellOrKeepCalculator.Advise(new SellOrKeepInput
        {
            AnimalId = animal.Id,
            TagId = "T-1",
            AcquisitionDate = new DateOnly(2026, 8, 1),
            Today = new DateOnly(2026, 11, 27),
            Weighings = [new(new DateOnly(2026, 10, 28), 282m), new(new DateOnly(2026, 11, 27), 300m)],
            Cost = new AnimalCostBreakdown(80_000m, 0m, 0m, 0m, 0m),
            FeedCostInWindowBdt = 30m * 150m,
            Price = new AnimalPrice(340m, AnimalPriceSource.FarmLiveRate),
        });
        var advisor = Substitute.For<IAnimalSellOrKeepAdvisor>();
        advisor.AdviseAnimalAsync(animal.Id, null, Arg.Any<CancellationToken>()).Returns(advice);

        var valuation = new AnimalValuationService(new TestRulesProvider());
        var sut = new GetAnimalFinancialSnapshotQueryHandler(animals, ledgers, resolver, valuation, advisor);

        var result = await sut.Handle(new GetAnimalFinancialSnapshotQuery(animal.Id), CancellationToken.None);

        var rate = valuation.LiveWeightRatePerKgBdt;
        result.TotalInvestmentBdt.Should().Be(80_000m, "today's investment is the cost to date, not the purchase price");
        result.EstimatedMarketValueBdt.Should().Be(300m * rate);
        result.CurrentProfitMarginBdt.Should().Be((300m * rate) - 80_000m);

        // The next 30/60 days' running cost = days 1..30 / 1..60 of the Sell-or-keep projection.
        result.Projected30DayCostBdt.Should().Be(advice.Projection[30].CostBdt - advice.Projection[0].CostBdt);
        result.Projected60DayCostBdt.Should().Be(advice.Projection[60].CostBdt - advice.Projection[0].CostBdt);
        result.Projected30DayCostBdt.Should().BeInRange(30m * 150m, 30m * 150m * 1.1m,
            "the actual ৳150/day feed run-rate, growing slightly with weight — not the plan's hard-coded grass/other/labour");
    }
}
