using Farm360.Application.Analytics.Services;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Analytics;

public sealed class HerdMetricsTests
{
    private static readonly DateOnly MonthStart = new(2026, 9, 1);
    private static readonly DateOnly MonthEnd = new(2026, 9, 30);

    // ── On-farm status group ────────────────────────────────────────────────

    [Theory]
    [InlineData(AnimalStatus.Active, true)]
    [InlineData(AnimalStatus.Quarantined, true)]
    [InlineData(AnimalStatus.Sold, false)]
    [InlineData(AnimalStatus.Slaughtered, false)]
    [InlineData(AnimalStatus.Dead, false)]
    [InlineData(AnimalStatus.Transferred, false)]
    public void IsOnFarm_MatchesOnFarmArray(AnimalStatus status, bool expected)
    {
        AnimalStatusGroups.IsOnFarm(status).Should().Be(expected);
        AnimalStatusGroups.OnFarm.Contains(status).Should().Be(expected);
    }

    // ── ADG ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AverageDailyGain_AveragesPerAnimalGains_NotFirstAndLastOfDifferentAnimals()
    {
        var heavy = Guid.NewGuid();
        var light = Guid.NewGuid();
        var weighIns = new[]
        {
            // A light animal weighed first and a heavy one weighed last must not look like a
            // 300 kg gain: each animal's own gain is used.
            new HerdMetrics.WeighIn(light, new DateOnly(2026, 9, 1), 100m),
            new HerdMetrics.WeighIn(light, new DateOnly(2026, 9, 11), 105m),   // 0.5 kg/day
            new HerdMetrics.WeighIn(heavy, new DateOnly(2026, 9, 5), 400m),
            new HerdMetrics.WeighIn(heavy, new DateOnly(2026, 9, 25), 420m),   // 1.0 kg/day
        };

        HerdMetrics.AverageDailyGain(weighIns, MonthStart, MonthEnd).Should().Be(0.75);
    }

    [Fact]
    public void AverageDailyGain_IgnoresAnimalsWithSingleWeighInOrSameDay()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var weighIns = new[]
        {
            new HerdMetrics.WeighIn(a, new DateOnly(2026, 9, 3), 200m),
            new HerdMetrics.WeighIn(b, new DateOnly(2026, 9, 4), 210m),
            new HerdMetrics.WeighIn(b, new DateOnly(2026, 9, 4), 212m),
        };

        HerdMetrics.AverageDailyGain(weighIns, MonthStart, MonthEnd).Should().BeNull();
    }

    [Fact]
    public void AverageDailyGain_NoDataInWindow_ReturnsNull_NotAnEstimate()
    {
        var a = Guid.NewGuid();
        var weighIns = new[]
        {
            new HerdMetrics.WeighIn(a, new DateOnly(2026, 8, 1), 200m),
            new HerdMetrics.WeighIn(a, new DateOnly(2026, 8, 20), 215m),
        };

        HerdMetrics.AverageDailyGain(weighIns, MonthStart, MonthEnd).Should().BeNull();
    }

    [Fact]
    public void AverageDailyGain_KeepsRealWeightLoss()
    {
        var a = Guid.NewGuid();
        var weighIns = new[]
        {
            new HerdMetrics.WeighIn(a, new DateOnly(2026, 9, 1), 200m),
            new HerdMetrics.WeighIn(a, new DateOnly(2026, 9, 11), 195m),
        };

        HerdMetrics.AverageDailyGain(weighIns, MonthStart, MonthEnd).Should().Be(-0.5);
    }

    // ── Average head count ─────────────────────────────────────────────────

    [Fact]
    public void AverageHeadCount_UsesHeadDaysOverDays()
    {
        var animals = new[]
        {
            new HerdMetrics.Residency(new DateOnly(2026, 1, 1), null),                    // all 30 days
            new HerdMetrics.Residency(new DateOnly(2026, 9, 16), null),                   // 15 days
            new HerdMetrics.Residency(new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 16)), // sold on the 16th: 15 days
            new HerdMetrics.Residency(new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 1)),  // gone before the month
            new HerdMetrics.Residency(new DateOnly(2026, 10, 1), null),                   // arrived after
        };

        HerdMetrics.AverageHeadCount(animals, MonthStart, MonthEnd).Should().Be(2m);
    }

    [Fact]
    public void AverageHeadCount_EmptyHerd_IsZero()
    {
        HerdMetrics.AverageHeadCount([], MonthStart, MonthEnd).Should().Be(0m);
    }

    [Fact]
    public void ExitDate_OnFarmAnimalHasNoExit_SoldUsesSaleDate_DeadUsesDeathDate()
    {
        var sale = new DateOnly(2026, 9, 10);
        var death = new DateOnly(2026, 9, 12);

        HerdMetrics.ExitDate(AnimalStatus.Active, null, null, null).Should().BeNull();
        HerdMetrics.ExitDate(AnimalStatus.Quarantined, null, null, DateTime.UtcNow).Should().BeNull();
        HerdMetrics.ExitDate(AnimalStatus.Sold, sale, null, new DateTime(2026, 11, 1)).Should().Be(sale);
        HerdMetrics.ExitDate(AnimalStatus.Dead, null, death, new DateTime(2026, 11, 1)).Should().Be(death);
        HerdMetrics.ExitDate(AnimalStatus.Transferred, null, null, new DateTime(2026, 9, 20)).Should().Be(new DateOnly(2026, 9, 20));
    }
}
