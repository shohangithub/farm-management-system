using Farm360.Application.Weather;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Weather;

/// <summary>THI maths, heat-stress band edges, weather codes, every advice rule, best hours and the bell alerts.</summary>
public sealed class CattleWeatherRulesTests
{
    private static readonly DateOnly Today = new(2026, 5, 10);

    private static WeatherDayDto Day(
        DateOnly date, int peakThi = 70, double max = 30, double min = 22, double rainMm = 0, int? chance = 0,
        int code = 1, double? gust = 15, double? humidity = 60, string? hotFrom = null, string? hotTo = null) =>
        new(date, max, min, rainMm, chance, code, "x", "x", "x", 10, gust, humidity, peakThi, CattleHeatStress.Band(peakThi), hotFrom, hotTo);

    private static List<string> Codes(IEnumerable<WeatherAdviceDto> advice) => advice.Select(a => a.Code).ToList();

    // ── THI ─────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(30, 70, 81.38)]
    [InlineData(25, 50, 71.775)]
    [InlineData(35, 80, 90.93)]
    [InlineData(20, 40, 64.7)]
    public void Thi_MatchesTheNrcFormula(double temperature, double humidity, double expected) =>
        CattleHeatStress.Thi(temperature, humidity).Should().BeApproximately(expected, 0.01);

    [Fact]
    public void Thi_AtFullHumidity_IsTheTemperatureInFahrenheit() =>
        CattleHeatStress.Thi(30, 100).Should().BeApproximately(86, 1e-9);

    [Fact]
    public void Thi_ClampsHumidity() =>
        CattleHeatStress.Thi(30, 140).Should().Be(CattleHeatStress.Thi(30, 100));

    [Theory]
    [InlineData(71.4, HeatStressBand.Comfortable)]
    [InlineData(71.5, HeatStressBand.Mild)]
    [InlineData(72, HeatStressBand.Mild)]
    [InlineData(78, HeatStressBand.Mild)]
    [InlineData(78.49, HeatStressBand.Mild)]
    [InlineData(78.5, HeatStressBand.Moderate)]
    [InlineData(79, HeatStressBand.Moderate)]
    [InlineData(88, HeatStressBand.Moderate)]
    [InlineData(89, HeatStressBand.Severe)]
    [InlineData(98, HeatStressBand.Severe)]
    [InlineData(98.4, HeatStressBand.Severe)]
    [InlineData(98.5, HeatStressBand.Danger)]
    [InlineData(99, HeatStressBand.Danger)]
    [InlineData(40, HeatStressBand.Comfortable)]
    public void Band_Edges(double thi, HeatStressBand expected) => CattleHeatStress.Band(thi).Should().Be(expected);

    // ── Weather codes ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "Clear sky", "wb_sunny")]
    [InlineData(2, "Partly cloudy", "wb_cloudy")]
    [InlineData(45, "Fog", "blur_on")]
    [InlineData(63, "Rain", "umbrella")]
    [InlineData(82, "Heavy showers", "umbrella")]
    [InlineData(95, "Thunderstorm", "flash_on")]
    [InlineData(1234, "Unknown weather", "help_outline")]
    public void WeatherCodes_MapToFriendlyTextAndIcon(int code, string english, string icon)
    {
        var condition = WeatherCodes.Describe(code);
        condition.English.Should().Be(english);
        condition.Icon.Should().Be(icon);
        condition.Bangla.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(95, true)]
    [InlineData(96, true)]
    [InlineData(99, true)]
    [InlineData(65, false)]
    [InlineData(0, false)]
    public void WeatherCodes_Thunderstorm(int code, bool expected) => WeatherCodes.IsThunderstorm(code).Should().Be(expected);

    // ── Advice rules ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Heat_Moderate_GivesWaterFeedShadeBathAndWorkAdvice_ButNoVetCall()
    {
        var advice = WeatherAnalyzer.Advise([Day(Today, peakThi: 82, hotFrom: "11:00", hotTo: "16:00")], Today);

        Codes(advice).Should().Contain(["heat.water", "heat.feed", "heat.shade", "heat.bathe", "heat.work"]);
        Codes(advice).Should().NotContain(["heat.signs", "heat.electrolytes", "good.day"]);
        var work = advice.Single(a => a.Code == "heat.work");
        work.Action.Should().Contain("{from}").And.Contain("{to}");
        work.Params["from"].Should().Be("11:00");
        work.Params["to"].Should().Be("16:00");
        work.Window.Should().Be(WeatherWindows.Today);
        work.Severity.Should().Be(WeatherAdviceSeverity.Warning);
    }

    [Fact]
    public void Heat_Severe_AddsElectrolytesAndHeatSigns()
    {
        var advice = WeatherAnalyzer.Advise([Day(Today, peakThi: 92)], Today);
        Codes(advice).Should().Contain(["heat.signs", "heat.electrolytes", "heat.water"]);
        advice.Single(a => a.Code == "heat.signs").Severity.Should().Be(WeatherAdviceSeverity.Danger);
        advice.Single(a => a.Code == "heat.signs").Params["thi"].Should().Be("92");
    }

    [Fact]
    public void Heat_Mild_GivesNoHeatAdvice() =>
        Codes(WeatherAnalyzer.Advise([Day(Today, peakThi: 78)], Today)).Should().NotContain(c => c.StartsWith("heat.", StringComparison.Ordinal));

    [Theory]
    [InlineData(0, 70, true)]
    [InlineData(20, 10, true)]
    [InlineData(19.9, 69, false)]
    public void Rain_FiresOnSeventyPercentOrTwentyMillimetres(double mm, int chance, bool fires)
    {
        var codes = Codes(WeatherAnalyzer.Advise([Day(Today, rainMm: mm, chance: chance)], Today));
        if (fires)
            codes.Should().Contain(["rain.floor", "rain.drains", "rain.feed", "rain.pests"]);
        else
            codes.Should().NotContain(c => c.StartsWith("rain.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(11.9, true)]
    [InlineData(12, false)]
    public void Cold_FiresBelowTwelveDegrees(double min, bool fires)
    {
        var codes = Codes(WeatherAnalyzer.Advise([Day(Today, min: min, max: 22)], Today));
        if (fires)
            codes.Should().Contain(["cold.cover", "cold.draught", "cold.feed", "cold.nobath"]);
        else
            codes.Should().NotContain(c => c.StartsWith("cold.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(50, 3, true)]
    [InlineData(49, 3, false)]
    [InlineData(20, 95, true)]
    [InlineData(20, 99, true)]
    public void Storm_FiresOnStrongGustsOrThunder(double gust, int code, bool fires)
    {
        var advice = WeatherAnalyzer.Advise([Day(Today, gust: gust, code: code)], Today);
        if (fires)
        {
            Codes(advice).Should().Contain(["storm.roof", "storm.inside"]);
            advice[0].Code.Should().StartWith("storm.", "danger comes first");
        }
        else
        {
            Codes(advice).Should().NotContain(c => c.StartsWith("storm.", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void HumidWarm_ThreeDaysInARow_GivesOnePairForTheRun()
    {
        var days = new[]
        {
            Day(Today, humidity: 85, max: 30), Day(Today.AddDays(1), humidity: 82, max: 29), Day(Today.AddDays(2), humidity: 80, max: 28),
            Day(Today.AddDays(3), humidity: 60, max: 30),
        };
        var advice = WeatherAnalyzer.Advise(days, Today);

        var skin = advice.Single(a => a.Code == "humid.skin");
        skin.FromDate.Should().Be(Today);
        skin.ToDate.Should().Be(Today.AddDays(2));
        skin.Params["days"].Should().Be("3");
        advice.Should().ContainSingle(a => a.Code == "humid.mould");
    }

    [Fact]
    public void HumidWarm_TwoDaysOrCoolDays_GivesNothing()
    {
        WeatherAnalyzer.Advise([Day(Today, humidity: 90), Day(Today.AddDays(1), humidity: 90), Day(Today.AddDays(2), humidity: 70)], Today)
            .Should().NotContain(a => a.Rule == "humid");
        WeatherAnalyzer.Advise([Day(Today, humidity: 90, max: 25), Day(Today.AddDays(1), humidity: 90, max: 25), Day(Today.AddDays(2), humidity: 90, max: 25)], Today)
            .Should().NotContain(a => a.Rule == "humid");
    }

    [Fact]
    public void GoodDay_OnlyForCalmMildTodayOrTomorrow()
    {
        var advice = WeatherAnalyzer.Advise([Day(Today, peakThi: 74), Day(Today.AddDays(1), peakThi: 70), Day(Today.AddDays(3), peakThi: 70)], Today);
        advice.Where(a => a.Code == "good.day").Select(a => a.Window).Should().BeEquivalentTo([WeatherWindows.Today, WeatherWindows.Tomorrow]);
        advice.Single(a => a.Window == WeatherWindows.Today).Severity.Should().Be(WeatherAdviceSeverity.Good);

        Codes(WeatherAnalyzer.Advise([Day(Today, peakThi: 80)], Today)).Should().NotContain("good.day");
        Codes(WeatherAnalyzer.Advise([Day(Today, chance: 50)], Today)).Should().NotContain("good.day");
        Codes(WeatherAnalyzer.Advise([Day(Today, min: 10)], Today)).Should().NotContain("good.day");
    }

    [Fact]
    public void LaterDays_AreMergedPerItem_WithTheWorstValues()
    {
        var days = new[] { Day(Today), Day(Today.AddDays(1)), Day(Today.AddDays(3), peakThi: 83), Day(Today.AddDays(5), peakThi: 86) };
        var water = WeatherAnalyzer.Advise(days, Today).Where(a => a.Code == "heat.water").ToList();

        water.Should().ContainSingle();
        water[0].Window.Should().Be(WeatherWindows.Later);
        water[0].FromDate.Should().Be(Today.AddDays(3));
        water[0].ToDate.Should().Be(Today.AddDays(5));
        water[0].Params["thi"].Should().Be("86");
    }

    [Fact]
    public void Advice_IsOrderedTodayTomorrowLater()
    {
        var advice = WeatherAnalyzer.Advise([Day(Today.AddDays(4), peakThi: 90), Day(Today.AddDays(1), rainMm: 30), Day(Today, min: 10)], Today);
        advice.Select(a => a.Window).Should().ContainInOrder(WeatherWindows.Today, WeatherWindows.Tomorrow, WeatherWindows.Later);
        advice[0].Rule.Should().Be("cold");
    }

    [Fact]
    public void TopAdvice_IsTheMostUrgentNearItem_AndGoodNewsOnlyWhenCalm()
    {
        var severe = WeatherAnalyzer.Advise([Day(Today, peakThi: 93), Day(Today.AddDays(1), peakThi: 70)], Today);
        var top = WeatherAnalyzer.TopAdvice(severe);
        top.Should().HaveCount(2);
        top[0].Code.Should().Be("heat.signs");
        top.Should().NotContain(a => a.Code == "good.day");

        var calm = WeatherAnalyzer.TopAdvice(WeatherAnalyzer.Advise([Day(Today, peakThi: 70)], Today));
        calm.Should().ContainSingle().Which.Code.Should().Be("good.day");

        WeatherAnalyzer.TopAdvice(WeatherAnalyzer.Advise([Day(Today.AddDays(4), peakThi: 95)], Today)).Should().BeEmpty("later days are not top advice");
    }

    // ── Best hours ──────────────────────────────────────────────────────────────────────────

    private static WeatherHourDto Hour(DateTime time, int thi, int? rain = 0) =>
        new(time, 30, 60, rain, thi, CattleHeatStress.Band(thi));

    [Fact]
    public void BestHours_PicksTheCoolestDryDaylightHoursLeftToday_InTimeOrder()
    {
        var day = Today.ToDateTime(TimeOnly.MinValue);
        var thi = new[] { 70, 70, 70, 70, 70, 70, 72, 73, 75, 78, 80, 82, 84, 85, 85, 84, 82, 80, 78, 76, 75, 74, 73, 72 };
        var hours = thi.Select((t, h) => Hour(day.AddHours(h), t, h == 8 ? 60 : 10)).ToList();

        var best = WeatherAnalyzer.BestHours(hours, new Dictionary<DateOnly, WeatherDay>(), day.AddHours(7).AddMinutes(20));

        best.Should().NotBeNull();
        best!.IsToday.Should().BeTrue();
        best.Hours.Select(h => h.Time.Hour).Should().Equal([7, 9, 10], "07:00 (73), 09:00 (78), 10:00 (80, earlier than 17:00); 08:00 has a 60% rain chance");
    }

    [Fact]
    public void BestHours_UseSunriseAndSunsetWhenKnown()
    {
        var day = Today.ToDateTime(TimeOnly.MinValue);
        var hours = Enumerable.Range(0, 24).Select(h => Hour(day.AddHours(h), 70)).ToList();
        var days = new Dictionary<DateOnly, WeatherDay>
        {
            [Today] = new(Today, 30, 20, 0, 0, 0, 5, 10, 60, day.AddHours(6).AddMinutes(21), day.AddHours(17).AddMinutes(10)),
        };

        var best = WeatherAnalyzer.BestHours(hours, days, day.AddHours(15));
        best!.Hours.Select(h => h.Time.Hour).Should().Equal([15, 16], "16:00–17:00 ends before sunset + 30 min, 17:00 does not");
    }

    [Fact]
    public void BestHours_InTheEvening_FallBackToTomorrow()
    {
        var day = Today.ToDateTime(TimeOnly.MinValue);
        var hours = Enumerable.Range(0, 48).Select(h => Hour(day.AddHours(h), 70 + (h % 24 > 12 ? 10 : 0))).ToList();

        var best = WeatherAnalyzer.BestHours(hours, new Dictionary<DateOnly, WeatherDay>(), day.AddHours(20));

        best!.IsToday.Should().BeFalse();
        best.Date.Should().Be(Today.AddDays(1));
        best.Hours.Select(h => h.Time.Hour).Should().Equal([6, 7, 8]);
    }

    // ── Bell alerts ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Alerts_OnlyTodayAndTomorrow_OnlyRealEvents_StormFirst()
    {
        var days = new[]
        {
            Day(Today, peakThi: 90, rainMm: 25),
            Day(Today.AddDays(1), gust: 60, min: 11),
            Day(Today.AddDays(2), peakThi: 99, rainMm: 80),
        };

        var alerts = WeatherAnalyzer.Alerts(days, Today);

        alerts.Select(a => (a.Kind, a.Window)).Should().Equal(
            (WeatherAnalyzer.AlertStorm, WeatherWindows.Tomorrow),
            (WeatherAnalyzer.AlertHeat, WeatherWindows.Today),
            (WeatherAnalyzer.AlertRain, WeatherWindows.Today),
            (WeatherAnalyzer.AlertCold, WeatherWindows.Tomorrow));
        alerts[0].Value.Should().Be("60");
    }

    [Fact]
    public void Alerts_ModerateHeatAndLightRain_AreNotBellWorthy() =>
        WeatherAnalyzer.Alerts([Day(Today, peakThi: 85, rainMm: 5, chance: 40), Day(Today.AddDays(1), peakThi: 88)], Today).Should().BeEmpty();
}
