using Farm360.Application.Common.Interfaces;
using Farm360.Application.InAppNotifications;
using Farm360.Application.Weather;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Weather;

/// <summary>The daily weather bell alert: one per farm, user and day; only for real events; 6:30 local start.</summary>
public sealed class WeatherAlertTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 5, 10);

    private sealed class FakeStore : INotificationStore
    {
        public List<Notification> Saved { get; } = [];
        private List<Notification> Pending { get; } = [];

        public Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Notification>>(Saved.Where(n => n.UserId == userId).Take(take).ToList());
        public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<Notification?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.FirstOrDefault(n => n.UserId == userId && n.Id == id));
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlySet<(Guid UserId, string DedupKey)>> GetExistingKeysAsync(
            IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<(Guid, string)>>(Saved
                .Where(n => n.DedupKey != null && userIds.Contains(n.UserId) && dedupKeys.Contains(n.DedupKey))
                .Select(n => (n.UserId, n.DedupKey!)).ToHashSet());

        public void Add(Notification notification) => Pending.Add(notification);

        public void Commit()
        {
            Saved.AddRange(Pending);
            Pending.Clear();
        }
    }

    private static WeatherDayDto Day(DateOnly date, int peakThi = 70, double rainMm = 0, double min = 22, double gust = 10) =>
        new(date, 32, min, rainMm, 0, 1, "x", "x", "x", 10, gust, 60, peakThi, CattleHeatStress.Band(peakThi), null, null);

    private static FarmWeatherDto Weather(Farm farm, params WeatherDayDto[] days)
    {
        var alerts = WeatherAnalyzer.Alerts(days, Today);
        var current = new WeatherCurrentDto(Today.ToDateTime(new TimeOnly(7, 0)), 30, 32, 60, 0, 5, 10, 1, "Mostly clear", "x", "wb_sunny", 80, HeatStressBand.Moderate);
        var report = new WeatherReportDto(Today.ToDateTime(new TimeOnly(7, 0)), Today, current, null, null, null, [], days, [], [], null, alerts);
        return new FarmWeatherDto(farm.Id, farm.BranchId, false, true, "Open-Meteo", new WeatherLocationDto(23.81, 90.41), DateTime.UtcNow, report);
    }

    private static (WeatherAlertService Service, FakeStore Store, Mock<IFarmWeatherService> Weather) Service(Farm farm, FarmWeatherDto weather, IReadOnlyList<Guid> users)
    {
        var weatherService = new Mock<IFarmWeatherService>();
        weatherService.Setup(w => w.GetForFarmAsync(farm, It.IsAny<CancellationToken>())).ReturnsAsync(weather);
        var recipients = new Mock<IHealthReminderRecipients>();
        recipients.Setup(r => r.GetUserIdsAsync(TenantGuid, farm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(users);
        var store = new FakeStore();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(store.Commit).ReturnsAsync(1);
        var service = new WeatherAlertService(weatherService.Object, recipients.Object, store, uow.Object, NullLogger<WeatherAlertService>.Instance);
        return (service, store, weatherService);
    }

    [Fact]
    public async Task SevereHeat_CreatesOneAlertPerUser_AndRunningAgainCreatesNothing()
    {
        var farm = WeatherProviderAndServiceTests.NewFarm(23.81, 90.41);
        var owner = Guid.NewGuid();
        var worker = Guid.NewGuid();
        var (service, store, weather) = Service(farm, Weather(farm, Day(Today, peakThi: 92), Day(Today.AddDays(1), rainMm: 30)), [owner, worker]);

        var created = await service.GenerateForFarmAsync(TenantGuid, farm, Today);

        created.Should().HaveCount(2);
        created.Select(n => n.UserId).Should().BeEquivalentTo([owner, worker]);
        var alert = created[0];
        alert.Category.Should().Be(WeatherAlertService.Category);
        alert.Link.Should().Be("/weather");
        alert.FarmId.Should().Be(farm.Id);
        alert.DedupKey.Should().Be(WeatherAlertService.DedupKey(farm.Id, Today));
        alert.Title.Should().Be("Weather alerts ({count})");
        alert.Body.Should().StartWith("Severe heat today");
        var dto = NotificationDto.From(alert);
        dto.Params["value"].Should().Be("92");
        dto.Params["count"].Should().Be("2");

        (await service.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().BeEmpty();
        (await service.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().BeEmpty();
        store.Saved.Should().HaveCount(2);
        weather.Verify(w => w.GetForFarmAsync(farm, It.IsAny<CancellationToken>()), Times.Once, "later runs stop before calling the provider");
    }

    [Fact]
    public async Task NewUserLaterInTheDay_GetsTheAlert_OthersDoNotGetASecondOne()
    {
        var farm = WeatherProviderAndServiceTests.NewFarm(23.81, 90.41);
        var first = Guid.NewGuid();
        var users = new List<Guid> { first };
        var (service, store, _) = Service(farm, Weather(farm, Day(Today.AddDays(1), gust: 70)), users);

        (await service.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().ContainSingle()
            .Which.Body.Should().StartWith("Storm risk tomorrow");
        users.Add(Guid.NewGuid());
        (await service.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().ContainSingle().Which.UserId.Should().Be(users[1]);
        store.Saved.Should().HaveCount(2);
    }

    [Fact]
    public async Task CalmOrModerateWeather_CreatesNothing()
    {
        var farm = WeatherProviderAndServiceTests.NewFarm(23.81, 90.41);
        var (service, store, _) = Service(farm, Weather(farm, Day(Today, peakThi: 85), Day(Today.AddDays(1))), [Guid.NewGuid()]);

        (await service.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().BeEmpty();
        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task WeatherUnavailable_OrNoLocation_OrNoRecipients_CreatesNothing()
    {
        var farm = WeatherProviderAndServiceTests.NewFarm(23.81, 90.41);
        var (unavailable, _, _) = Service(farm, FarmWeatherDto.Unavailable(farm.Id, farm.BranchId, null), [Guid.NewGuid()]);
        (await unavailable.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().BeEmpty();

        var noLocation = WeatherProviderAndServiceTests.NewFarm(null, null);
        var (service, _, weather) = Service(noLocation, Weather(noLocation, Day(Today, peakThi: 95)), [Guid.NewGuid()]);
        (await service.GenerateForFarmAsync(TenantGuid, noLocation, Today)).Should().BeEmpty();
        weather.Verify(w => w.GetForFarmAsync(It.IsAny<Farm>(), It.IsAny<CancellationToken>()), Times.Never);

        var (nobody, _, nobodyWeather) = Service(farm, Weather(farm, Day(Today, peakThi: 95)), []);
        (await nobody.GenerateForFarmAsync(TenantGuid, farm, Today)).Should().BeEmpty();
        nobodyWeather.Verify(w => w.GetForFarmAsync(It.IsAny<Farm>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 29, false)] // 06:29 Dhaka
    [InlineData(0, 30, true)]  // 06:30 Dhaka
    public async Task TenantJob_StartsAtHalfPastSixFarmTime_AndSkipsTheDemo(int utcHour, int minute, bool runs)
    {
        var farm = WeatherProviderAndServiceTests.NewFarm(23.81, 90.41);
        var farms = new Mock<IFarmRepository>();
        farms.Setup(f => f.GetAllByTenantAsync(TenantGuid, It.IsAny<CancellationToken>())).ReturnsAsync([farm]);
        var alerts = new Mock<IWeatherAlertService>();
        alerts.Setup(a => a.GenerateForFarmAsync(TenantGuid, farm, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var tenant = new Mock<ITenantService>();
        tenant.SetupGet(t => t.TenantSlug).Returns("green-acres");
        var clock = new Mock<IFarmClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 5, 10, utcHour, minute, 0, DateTimeKind.Utc));
        clock.Setup(c => c.ZoneFor(TenantGuid, (Guid?)farm.Id)).Returns(Farm360.Application.Common.Time.FarmTimeZones.Default);

        var handler = new GenerateTenantWeatherAlertsCommandHandler(farms.Object, alerts.Object, tenant.Object, clock.Object);
        await handler.Handle(new GenerateTenantWeatherAlertsCommand(TenantGuid), CancellationToken.None);

        alerts.Verify(a => a.GenerateForFarmAsync(TenantGuid, farm, new DateOnly(2026, 5, 10), It.IsAny<CancellationToken>()), runs ? Times.Once() : Times.Never());

        tenant.SetupGet(t => t.TenantSlug).Returns(Farm360.Application.Demo.DemoConstants.SlugPrefix + "abc");
        (await handler.Handle(new GenerateTenantWeatherAlertsCommand(TenantGuid, IgnoreAlertTime: true), CancellationToken.None)).Should().Be(0);
    }
}
