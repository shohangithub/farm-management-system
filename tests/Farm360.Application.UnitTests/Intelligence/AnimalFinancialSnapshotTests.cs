using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;
using Farm360.Application.Intelligence.Services;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Intelligence.Projections;
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

        var valuation = new AnimalValuationService(new TestRulesProvider());
        var sut = new GetAnimalFinancialSnapshotQueryHandler(animals, ledgers, resolver, valuation);

        var result = await sut.Handle(new GetAnimalFinancialSnapshotQuery(animal.Id), CancellationToken.None);

        var rate = valuation.LiveWeightRatePerKgBdt;
        result.TotalInvestmentBdt.Should().Be(80_000m, "today's investment is the cost to date, not the purchase price");
        result.EstimatedMarketValueBdt.Should().Be(300m * rate);
        result.CurrentProfitMarginBdt.Should().Be((300m * rate) - 80_000m);

        // The next 30/60 days' running cost = days 1..30 / 1..60 of a projection starting today.
        var expected = FatteningProjectionCalculator.Calculate(ProjectionInputsFactory.FromToday(defaults, 120));
        var day30 = expected.Days.Single(d => d.Day == 30);
        var day60 = expected.Days.Single(d => d.Day == 60);
        result.Projected30DayCostBdt.Should().Be(Math.Round(day30.CumulativeCostBdt, 2));
        result.Projected60DayCostBdt.Should().Be(Math.Round(day60.CumulativeCostBdt, 2));
        day30.TotalInvestmentBdt.Should().Be(80_000m + day30.CumulativeCostBdt);
        result.Projected30DayCostBdt.Should().BeGreaterThan(30m * 6m * 50m, "it includes grass, other and labour, not feed alone");
    }
}
