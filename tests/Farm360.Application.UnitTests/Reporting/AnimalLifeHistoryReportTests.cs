using System;
using System.Linq;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class AnimalLifeHistoryReportTests
{
    private static AnimalLifeHistoryReportDefinition.LifeFacts Facts(
        AnimalStatus status, decimal? salePrice, decimal? estimated, decimal feed = 20_000m, decimal owed = 0m) =>
        new(
            "B-04",
            status,
            new DateOnly(2026, 5, 1),
            status == AnimalStatus.Sold ? new DateOnly(2026, 11, 12) : null,
            195,
            new AnimalCostBreakdown(60_000m, feed, 2_000m, 3_000m, 1_000m),
            salePrice,
            estimated,
            "Karim",
            200m,
            320m,
            4,
            1_200m,
            190,
            1,
            2,
            3_000m,
            salePrice is { } p ? p - owed : 0m,
            owed,
            0m);

    [Fact]
    public void Sold_animal_shows_profit_against_its_full_cost()
    {
        var summary = AnimalLifeHistoryReportDefinition.Summarize(
            ReportingTestKit.Context(), Facts(AnimalStatus.Sold, 100_000m, null, owed: 15_000m));

        summary.Figures.Single(f => f.Label == "Total cost").Value.Should().Be("86,000 BDT");
        summary.Figures.Single(f => f.Label == "Sold for").Value.Should().Be("100,000 BDT");
        var profit = summary.Figures.Single(f => f.Label == "Profit");
        profit.Value.Should().Be("14,000 BDT");
        profit.Tone.Should().Be(ReportTone.Good);
        summary.Figures.Should().Contain(f => f.Label == "Buyer still owes" && f.Value == "15,000 BDT");

        summary.Insights[0].Should().StartWith("B-04 was sold for 100,000 BDT against a total cost of 86,000 BDT: a profit of 14,000 BDT");
        summary.Insights.Should().Contain(i => i.StartsWith("After the purchase, feed was the biggest cost", StringComparison.Ordinal));
        summary.Insights.Should().Contain(i => i.StartsWith("It gained 120.0 kg", StringComparison.Ordinal));
        summary.Insights.Should().Contain(i => i.Contains("break-even was", StringComparison.Ordinal));
    }

    [Fact]
    public void Animal_on_the_farm_shows_expected_result_and_break_even_price()
    {
        var summary = AnimalLifeHistoryReportDefinition.Summarize(
            ReportingTestKit.Context(), Facts(AnimalStatus.Active, null, 80_000m));

        summary.Figures.Single(f => f.Label == "Status").Value.Should().Be("On the farm");
        summary.Figures.Single(f => f.Label == "Expected loss").Tone.Should().Be(ReportTone.Bad);
        summary.Insights.Should().Contain(i => i.StartsWith("To break even it must sell for at least 86,000 BDT", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_feed_records_are_flagged()
    {
        var summary = AnimalLifeHistoryReportDefinition.Summarize(
            ReportingTestKit.Context(), Facts(AnimalStatus.Sold, 90_000m, null, feed: 0m));

        summary.Insights.Should().Contain(i => i.StartsWith("Check: no feed cost is recorded", StringComparison.Ordinal));
    }
}

public class AnimalLifeHistoryReportRunTests
{
    [Fact]
    public async Task Runs_for_a_sold_animal_and_asks_for_bounded_dates()
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var animal = Farm360.Domain.Livestock.Animal.Create(tenant, farm,
            Farm360.Domain.Livestock.ValueObjects.AnimalTag.Create("B-04", TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 5, 1), 60_000m, null);
        animal.Sell(100_000m, new DateOnly(2026, 11, 12), Guid.NewGuid(), "Karim", 320m);

        var animals = NSubstitute.Substitute.For<Farm360.Domain.Livestock.Repositories.IAnimalRepository>();
        animals.GetByIdWithWeightsAsync(animal.Id, Arg.Any<System.Threading.CancellationToken>()).Returns(animal);
        animals.GetMovementsByFarmAsync(farm, Arg.Any<System.Threading.CancellationToken>())
            .Returns(Array.Empty<(Guid, Guid?, DateTime, DateTime?)>());
        var breeds = NSubstitute.Substitute.For<Farm360.Domain.Livestock.Repositories.IBreedRepository>();
        breeds.GetAllAsync(Arg.Any<System.Threading.CancellationToken>()).Returns(Array.Empty<Farm360.Domain.Livestock.Breed>());
        var feed = NSubstitute.Substitute.For<Farm360.Domain.Feeding.Interfaces.Repositories.IAnimalFeedAllocationRepository>();
        feed.GetTotalsForAnimalAsync(animal.Id, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new Farm360.Domain.Feeding.Interfaces.Repositories.AnimalFeedTotals(animal.Id, 1_000m, 20_000m, 190));
        feed.GetDailyEatenByFarmAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns([new Farm360.Domain.Feeding.Interfaces.Repositories.AnimalFeedDay(animal.Id, new DateOnly(2026, 6, 3), 6m, 100m)]);
        var transactions = NSubstitute.Substitute.For<Farm360.Application.Finance.Repositories.IFinancialTransactionRepository>();
        transactions.GetManualVetPostingsAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Array.Empty<Farm360.Domain.Finance.FinancialTransaction>());
        var valuation = NSubstitute.Substitute.For<IAnimalValuationService>();
        valuation.CostToDate(animal, Arg.Any<Farm360.Domain.Finance.AnimalCostLedger?>(), Arg.Any<decimal>())
            .Returns(new AnimalCostBreakdown(60_000m, 20_000m, 0m, 3_000m, 1_000m));

        var report = new AnimalLifeHistoryReportDefinition(
            animals, breeds,
            NSubstitute.Substitute.For<Farm360.Domain.Farms.Repositories.IShedRepository>(),
            feed,
            NSubstitute.Substitute.For<Farm360.Domain.Health.Interfaces.Repositories.IMedicalTreatmentRepository>(),
            NSubstitute.Substitute.For<Farm360.Domain.Health.Interfaces.Repositories.IVaccinationRepository>(),
            transactions,
            NSubstitute.Substitute.For<Farm360.Domain.Finance.Interfaces.IAnimalOverheadAllocationRepository>(),
            NSubstitute.Substitute.For<Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository>(),
            NSubstitute.Substitute.For<Farm360.Domain.Finance.Interfaces.IAnimalCostLedgerRepository>(),
            valuation,
            NSubstitute.Substitute.For<Farm360.Domain.Finance.Interfaces.ITradeBillRepository>());

        var data = await ReportingTestKit.RunAsync(report, ReportingTestKit.Context(
            new System.Collections.Generic.Dictionary<string, string?> { ["animalId"] = animal.Id.ToString() }));

        data.Summary.Should().NotBeNull();
        data.Summary!.Figures.Single(f => f.Label == "Profit").Value.Should().Be("16,000 BDT");
        data.GrandTotals.Should().Contain(c => Equals(c.V, 84_000m), "total cost equals the cost book");
        data.GrandTotals.Should().Contain(c => Equals(c.V, 100_000m), "the sale is the income");

        // No repository is asked for an open-ended "until the end of time" date it would have to step past.
        await transactions.Received().GetManualVetPostingsAsync(farm, Arg.Any<DateOnly>(),
            Arg.Is<DateOnly>(d => d < DateOnly.MaxValue), Arg.Any<System.Threading.CancellationToken>());
    }
}
