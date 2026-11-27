using Farm360.Application.Analytics.Queries;
using Farm360.Application.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Analytics;

/// <summary>
/// Health trends: 12 calendar months (zero-filled), totals over every record and the top illnesses.
/// </summary>
public sealed class HealthTrendsTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);
    private static readonly DateOnly From = new(2025, 12, 1);

    private static HealthTrendSourceData Data(
        IReadOnlyList<HealthMonthAggregate>? deaths = null,
        IReadOnlyList<HealthMonthAggregate>? treatments = null,
        IReadOnlyList<HealthIllnessCountDto>? illnesses = null,
        int onFarm = 0, int vaccinated = 0) =>
        new(deaths ?? [], treatments ?? [], illnesses ?? [], onFarm, vaccinated);

    [Fact]
    public async Task Handle_AsksForTheTwelveMonthWindowEndingToday()
    {
        var source = Substitute.For<IHealthTrendSource>();
        source.GetAsync(default, default, default, default, default, default).ReturnsForAnyArgs(Data());
        var clock = Substitute.For<IFarmClock>();
        clock.Today.Returns(Today);
        var farmId = Guid.NewGuid();

        var result = await new GetHealthTrendsQueryHandler(source, clock).Handle(new GetHealthTrendsQuery(farmId), CancellationToken.None);

        await source.Received(1).GetAsync(farmId, From, Today, new DateOnly(2026, 5, 26),
            GetHealthTrendsQueryHandler.TopIllnessCount, Arg.Any<CancellationToken>());
        result.From.Should().Be(From);
        result.To.Should().Be(Today);
    }

    [Fact]
    public void Build_ZeroFillsTwelveMonths_OldestFirst()
    {
        var result = GetHealthTrendsQueryHandler.Build(Data(deaths: [new(2026, 3, 2)]), From, Today);

        result.Months.Should().HaveCount(12);
        result.Months[0].Should().Be(new HealthTrendMonthDto(2025, 12, 0, 0, 0m));
        result.Months[^1].Year.Should().Be(2026);
        result.Months[^1].Month.Should().Be(11);
        result.Months.Single(m => m.Month == 3).Deaths.Should().Be(2);
        result.Months.Where(m => m.Month != 3).Should().OnlyContain(m => m.Deaths == 0);
    }

    [Fact]
    public void Build_TotalsCountEveryRecord_NotAPage()
    {
        // 150 deaths and 240 treatments: more than any page of the list endpoints would carry.
        var deaths = Enumerable.Range(0, 12).Select(i => new HealthMonthAggregate(From.AddMonths(i).Year, From.AddMonths(i).Month, i == 5 ? 95 : 5)).ToList();
        var treatments = new List<HealthMonthAggregate> { new(2026, 1, 140, 70_000m), new(2026, 10, 100, 30_500m) };

        var result = GetHealthTrendsQueryHandler.Build(Data(deaths, treatments), From, Today);

        result.TotalDeaths.Should().Be(150);
        result.TotalTreatments.Should().Be(240);
        result.TotalTreatmentCostBdt.Should().Be(100_500m);
        result.Months.Single(m => m.Year == 2026 && m.Month == 1).TreatmentCostBdt.Should().Be(70_000m);
    }

    [Fact]
    public void Build_IgnoresRowsOutsideTheWindow()
    {
        var result = GetHealthTrendsQueryHandler.Build(Data(deaths: [new(2025, 11, 9), new(2026, 11, 1)]), From, Today);

        result.TotalDeaths.Should().Be(1);
    }

    [Fact]
    public void Build_RanksIllnessesByCount_AndKeepsTheTopFive()
    {
        var illnesses = new List<HealthIllnessCountDto>
        {
            new("Fever", 3), new("FMD", 12), new(" Bloat ", 7), new("", 40), new("Diarrhoea", 7),
            new("LSD", 5), new("Pneumonia", 4),
        };

        var result = GetHealthTrendsQueryHandler.Build(Data(illnesses: illnesses), From, Today);

        result.TopIllnesses.Select(i => i.Name).Should().Equal("FMD", "Bloat", "Diarrhoea", "LSD", "Pneumonia");
        result.TopIllnesses[0].Count.Should().Be(12);
    }

    [Theory]
    [InlineData(0, 0, 0d)]
    [InlineData(40, 30, 75d)]
    [InlineData(3, 1, 33.3d)]
    [InlineData(10, 12, 100d)]
    public void Build_VaccinationCoverage(int onFarm, int vaccinated, double expected)
    {
        var result = GetHealthTrendsQueryHandler.Build(Data(onFarm: onFarm, vaccinated: vaccinated), From, Today);

        result.VaccinationCoveragePercent.Should().Be(expected);
    }
}
