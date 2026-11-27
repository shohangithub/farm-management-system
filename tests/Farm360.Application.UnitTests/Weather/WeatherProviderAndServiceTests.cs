using Farm360.Application.Common.Interfaces;
using Farm360.Application.Weather;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using Farm360.Infrastructure.Weather;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Weather;

/// <summary>Open-Meteo parsing (saved real answer, no network), the request URL, and the farm weather service.</summary>
public sealed class WeatherProviderAndServiceTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();

    private static string SampleJson() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Weather", "open-meteo-dhaka.json"));

    internal static WeatherForecast Sample() => OpenMeteoParser.Parse(SampleJson());

    // ── Parser ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parser_ReadsTheSavedDhakaAnswer()
    {
        var forecast = Sample();

        forecast.TimeZone.Should().Be("Asia/Dhaka");
        forecast.UtcOffsetSeconds.Should().Be(21600);
        forecast.Current.Time.Should().Be(new DateTime(2026, 11, 27, 16, 15, 0));
        forecast.Current.TemperatureC.Should().Be(26.7);
        forecast.Current.HumidityPercent.Should().Be(62);
        forecast.Current.ApparentTemperatureC.Should().Be(29.5);
        forecast.Current.WeatherCode.Should().Be(1);
        forecast.Current.WindGustsKmh.Should().Be(8.3);

        forecast.Hourly.Should().HaveCount(168);
        forecast.Hourly[0].Time.Should().Be(new DateTime(2026, 11, 27, 0, 0, 0));
        forecast.Hourly.Should().OnlyContain(h => h.PrecipitationProbability != null);

        forecast.Daily.Should().HaveCount(7);
        var first = forecast.Daily[0];
        first.Date.Should().Be(new DateOnly(2026, 11, 27));
        first.MaxTemperatureC.Should().Be(27.6);
        first.MinTemperatureC.Should().Be(18.1);
        first.HumidityMeanPercent.Should().Be(76);
        first.WindGustsMaxKmh.Should().Be(17.6);
        first.Sunrise.Should().Be(new DateTime(2026, 11, 27, 6, 21, 0));
        first.Sunset.Should().Be(new DateTime(2026, 11, 27, 17, 10, 0));
    }

    [Fact]
    public void Parser_SkipsNullHours_AndRejectsGarbage()
    {
        const string json = """
            {"utc_offset_seconds":21600,"timezone":"Asia/Dhaka",
             "current":{"time":"2026-11-27T10:00","temperature_2m":30,"relative_humidity_2m":70,"weather_code":2},
             "hourly":{"time":["2026-11-27T10:00","2026-11-27T11:00"],"temperature_2m":[30,null],"relative_humidity_2m":[70,null],"precipitation_probability":[null,null]},
             "daily":{"time":["2026-11-27"],"temperature_2m_max":[33],"temperature_2m_min":[24],"weather_code":[2]}}
            """;
        var forecast = OpenMeteoParser.Parse(json);
        forecast.Hourly.Should().ContainSingle().Which.PrecipitationProbability.Should().BeNull();
        forecast.Daily.Should().ContainSingle().Which.WindGustsMaxKmh.Should().BeNull();
        forecast.Current.ApparentTemperatureC.Should().Be(30, "falls back to the air temperature");

        var act = () => OpenMeteoParser.Parse("""{"error":true,"reason":"bad"}""");
        act.Should().Throw<WeatherUnavailableException>();
    }

    [Fact]
    public void Analyzer_OnTheDhakaSample_GivesBandsAdviceAndBestHours()
    {
        var report = WeatherAnalyzer.Analyze(Sample(), new DateTime(2026, 11, 27, 16, 15, 0));

        report.Today.Should().Be(new DateOnly(2026, 11, 27));
        report.Current.Thi.Should().Be(75, "26.7 °C at 62% → THI 75.4");
        report.Current.Band.Should().Be(HeatStressBand.Mild);
        report.Current.Condition.Should().Be("Mostly clear");
        report.Hourly.Should().HaveCount(48);
        report.Hourly[0].Time.Should().Be(new DateTime(2026, 11, 27, 16, 0, 0));
        report.Daily.Should().HaveCount(7);
        report.Daily.Should().OnlyContain(d => d.PeakBand <= HeatStressBand.Moderate);
        report.Advice.Should().NotBeEmpty();
        report.BestHours.Should().NotBeNull();
        report.BestHours!.Hours.Should().OnlyContain(h => h.Time.Hour >= 16 || !report.BestHours.IsToday);
    }

    // ── Request URL ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Url_SendsOnlyCoordinatesAndFields_AndTheKeyOnlyWhenSet()
    {
        var free = OpenMeteoWeatherProvider.BuildUrl(new WeatherOptions(), 23.8103, 90.4125).ToString();
        free.Should().StartWith("https://api.open-meteo.com/v1/forecast?latitude=23.81&longitude=90.41&");
        free.Should().Contain("timezone=Asia%2FDhaka").And.Contain("forecast_days=7").And.NotContain("apikey");
        free.Should().Contain("current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_gusts_10m");
        free.Should().Contain("precipitation_probability_max").And.Contain("wind_speed_10m_max");

        var paid = OpenMeteoWeatherProvider.BuildUrl(
            new WeatherOptions { BaseUrl = "https://customer-api.open-meteo.com/v1/forecast", ApiKey = "k-123" }, 22.5, 91.8).ToString();
        paid.Should().StartWith("https://customer-api.open-meteo.com/v1/forecast?latitude=22.50&longitude=91.80&");
        paid.Should().EndWith("&apikey=k-123");
    }

    // ── Service: location, cache, failures ─────────────────────────────────────────────────

    /// <summary>In-memory stand-in for the distributed cache (JSON round trip like Redis).</summary>
    internal sealed class FakeCache : ICacheService
    {
        private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(key, out var json) ? System.Text.Json.JsonSerializer.Deserialize<T>(json) : default);

        public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            _items[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_items.Remove(key));

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal static Farm NewFarm(double? latitude, double? longitude) =>
        Farm.Create(TenantGuid, Guid.NewGuid(), "F-01", "Green Acres", FarmType.Mixed, null, null, latitude, longitude, null, null, null, null, null);

    private static (FarmWeatherService Service, Mock<IWeatherProvider> Provider) Service(Farm farm, WeatherOptions? options = null)
    {
        var farms = new Mock<IFarmRepository>();
        farms.Setup(f => f.GetByIdAsync(TenantGuid, farm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(farm);
        var provider = new Mock<IWeatherProvider>();
        provider.SetupGet(p => p.Name).Returns("Open-Meteo");
        provider.Setup(p => p.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(Sample());
        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 11, 27, 10, 15, 0, DateTimeKind.Utc));
        var service = new FarmWeatherService(farms.Object, provider.Object, new FakeCache(), Options.Create(options ?? new WeatherOptions()),
            NullLogger<FarmWeatherService>.Instance, clock.Object);
        return (service, provider);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(23.8, null)]
    [InlineData(0d, 0d)]
    [InlineData(95d, 90d)]
    public async Task NoUsableCoordinates_AnswersNeedsLocation_WithoutCallingTheProvider(double? latitude, double? longitude)
    {
        var farm = NewFarm(latitude, longitude);
        var (service, provider) = Service(farm);

        var result = await service.GetAsync(TenantGuid, farm.Id);

        result!.NeedsLocation.Should().BeTrue();
        result.Available.Should().BeFalse();
        result.BranchId.Should().Be(farm.BranchId, "the web links to the farm edit page");
        result.Report.Should().BeNull();
        provider.Verify(p => p.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendsRoundedCoordinates_AndReusesTheCachedForecast()
    {
        var farm = NewFarm(23.81032, 90.41249);
        var (service, provider) = Service(farm);

        var first = await service.GetAsync(TenantGuid, farm.Id);
        var second = await service.GetAsync(TenantGuid, farm.Id);

        first!.Available.Should().BeTrue();
        first.NeedsLocation.Should().BeFalse();
        first.Location.Should().Be(new WeatherLocationDto(23.81, 90.41));
        first.Provider.Should().Be("Open-Meteo");
        first.Report!.LocalNow.Should().Be(new DateTime(2026, 11, 27, 16, 15, 0), "UTC 10:15 + 6 h");
        second!.Report!.Current.Thi.Should().Be(first.Report.Current.Thi);
        provider.Verify(p => p.GetForecastAsync(23.81, 90.41, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProviderFailure_IsWeatherUnavailable_NotAnError()
    {
        var farm = NewFarm(23.81, 90.41);
        var (service, provider) = Service(farm);
        provider.Setup(p => p.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WeatherUnavailableException("down"));

        var result = await service.GetAsync(TenantGuid, farm.Id);

        result!.Available.Should().BeFalse();
        result.NeedsLocation.Should().BeFalse();
        result.Location.Should().NotBeNull();
        result.Report.Should().BeNull();
    }

    [Fact]
    public async Task Disabled_IsUnavailable_WithoutCallingTheProvider()
    {
        var farm = NewFarm(23.81, 90.41);
        var (service, provider) = Service(farm, new WeatherOptions { Enabled = false });

        (await service.GetAsync(TenantGuid, farm.Id))!.Available.Should().BeFalse();
        provider.Verify(p => p.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Query_UnknownFarm_IsNotFound_AndAFarmWithoutLocation_IsNotAnError()
    {
        var farm = NewFarm(null, null);
        var weather = new Mock<IFarmWeatherService>();
        weather.Setup(w => w.GetAsync(TenantGuid, farm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(FarmWeatherDto.MissingLocation(farm.Id, farm.BranchId));
        var tenant = new Mock<ITenantService>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantGuid);
        var handler = new GetFarmWeatherQueryHandler(weather.Object, tenant.Object);

        (await handler.Handle(new GetFarmWeatherQuery(farm.Id), CancellationToken.None)).NeedsLocation.Should().BeTrue();
        var missing = () => handler.Handle(new GetFarmWeatherQuery(Guid.NewGuid()), CancellationToken.None);
        await missing.Should().ThrowAsync<Farm360.Application.Common.Exceptions.NotFoundException>();
    }
}
