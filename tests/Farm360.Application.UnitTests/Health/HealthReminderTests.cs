using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Tasks;
using Farm360.Application.InAppNotifications;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Health;
using Farm360.Domain.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Health;

/// <summary>Daily health reminders: grouping, targeting, idempotency and the 7:00 local start.</summary>
public sealed class HealthReminderTests
{
    private static readonly DateOnly Today = new(2026, 11, 27);
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid FarmGuid = Guid.NewGuid();
    private static readonly string[] FiveTags = ["B-01", "B-02", "B-03", "B-04", "B-05"];

    /// <summary>In-memory store that behaves like the unique (tenant, user, key) index.</summary>
    private sealed class FakeStore : INotificationStore
    {
        public List<Notification> Saved { get; } = [];
        public List<Notification> Pending { get; } = [];

        public Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Notification>>(Saved.Where(n => n.UserId == userId).Take(take).ToList());
        public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.Count(n => n.UserId == userId && !n.IsRead));
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

    private static HealthTaskRow Row(string tag, string task, DateOnly date, Guid? animalId = null)
    {
        var e = VaccinationEvent.Schedule(TenantGuid, animalId ?? Guid.NewGuid(), null, task, "", date, null);
        return new HealthTaskRow(e, tag, FarmGuid, null, null, null);
    }

    private static (HealthReminderService Service, FakeStore Store, Mock<INotificationService> Live) Service(
        IReadOnlyList<HealthTaskRow> rows, IReadOnlyList<Guid> users)
    {
        var tasks = new Mock<IHealthTaskSource>();
        tasks.Setup(t => t.GetPendingAsync(FarmGuid, Today.AddDays(1), It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var recipients = new Mock<IHealthReminderRecipients>();
        recipients.Setup(r => r.GetUserIdsAsync(TenantGuid, FarmGuid, It.IsAny<CancellationToken>())).ReturnsAsync(users);
        var store = new FakeStore();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(store.Commit).ReturnsAsync(1);
        var live = new Mock<INotificationService>();

        var service = new HealthReminderService(tasks.Object, recipients.Object, store, uow.Object,
            NullLogger<HealthReminderService>.Instance, live.Object);
        return (service, store, live);
    }

    [Fact]
    public async Task CreatesTodayTomorrowAndOverdue_ForEachTargetedUser_AndPushesThemLive()
    {
        var owner = Guid.NewGuid();
        var vet = Guid.NewGuid();
        var rows = new[]
        {
            Row("B-04", "Dewormer", Today), Row("B-07", "Dewormer", Today), Row("C-02", "FMD", Today),
            Row("B-09", "LSD", Today.AddDays(1)),
            Row("A-01", "Anthrax", Today.AddDays(-3)),
        };
        var (service, store, live) = Service(rows, [owner, vet]);

        var created = await service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today);

        created.Should().HaveCount(6, "three kinds × two users");
        created.Select(n => n.UserId).Distinct().Should().BeEquivalentTo([owner, vet], "only the users the farm's roles target");

        var today = created.First(n => n.Category == HealthReminderService.CategoryToday);
        today.Title.Should().Be("Health tasks for today");
        today.Body.Should().Be("{count} animals are due today: {items}");
        var dto = NotificationDto.From(today);
        dto.Params["count"].Should().Be("3");
        dto.Params["items"].Should().Be("Dewormer (B-04, B-07), FMD (C-02)");
        today.Link.Should().Be("/health/today?focus=today");
        today.FarmId.Should().Be(FarmGuid);

        created.Single(n => n.Category == HealthReminderService.CategoryOverdue && n.UserId == owner).Type.Should().Be(NotificationType.Warning);
        created.Single(n => n.Category == HealthReminderService.CategoryTomorrow && n.UserId == vet).Body.Should().Be("{count} animal is due tomorrow: {items}");

        live.Verify(l => l.SendToUserAsync(TenantGuid, It.IsAny<Guid>(), NotificationDto.LiveEventName, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
        store.Saved.Should().HaveCount(6);
    }

    [Fact]
    public async Task RunningAgainTheSameDay_CreatesNothing()
    {
        var (service, store, live) = Service([Row("B-04", "Dewormer", Today), Row("A-01", "FMD", Today.AddDays(-2))], [Guid.NewGuid()]);

        (await service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today)).Should().HaveCount(2);
        (await service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today)).Should().BeEmpty();
        (await service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today)).Should().BeEmpty();

        store.Saved.Should().HaveCount(2);
        live.Verify(l => l.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NoRecipientsOrNoTasks_CreatesNothing()
    {
        (await Service([Row("B-04", "Dewormer", Today)], []).Service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today)).Should().BeEmpty();
        (await Service([], [Guid.NewGuid()]).Service.GenerateForFarmAsync(TenantGuid, FarmGuid, Today)).Should().BeEmpty();
    }

    [Fact]
    public void Summary_ShowsThreeTagsPerTaskThenAnEllipsis()
    {
        var rows = FiveTags.Select(t => Row(t, "Dewormer", Today)).Append(Row("C-02", "FMD", Today));
        HealthReminderService.Summarise(rows).Should().Be("Dewormer (B-01, B-02, B-03…), FMD (C-02)");
    }

    [Fact]
    public void DedupKey_IsPerKindFarmAndDay()
    {
        HealthReminderService.DedupKey("today", FarmGuid, Today)
            .Should().Be($"health:today:{FarmGuid:N}:20261127")
            .And.NotBe(HealthReminderService.DedupKey("today", FarmGuid, Today.AddDays(1)));
    }

    // ── The tenant job: local 7:00, demo tenant skipped ────────────────────────

    private static (GenerateTenantHealthRemindersCommandHandler Handler, Mock<IHealthReminderService> Reminders) TenantJob(
        int localHour, string slug = "shapla")
    {
        var farms = new Mock<IFarmRepository>();
        var farm = Farm360.Domain.Farms.Farm.Create(TenantGuid, Guid.NewGuid(), "F-1", "Farm", Farm360.Domain.Farms.Enums.FarmType.Mixed,
            null, null, null, null, null, null, null, null, null);
        farms.Setup(f => f.GetAllByTenantAsync(TenantGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Farm360.Domain.Farms.Farm> { farm });

        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+6", TimeSpan.FromHours(6), "UTC+6", "UTC+6");
        var clock = new Mock<IFarmClock>();
        clock.Setup(c => c.ZoneFor(TenantGuid, It.IsAny<Guid?>())).Returns(zone);
        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 11, 27, localHour, 5, 0, DateTimeKind.Utc).AddHours(-6));

        var tenant = new Mock<ITenantService>();
        tenant.Setup(t => t.TenantSlug).Returns(slug);
        var reminders = new Mock<IHealthReminderService>();
        reminders.Setup(r => r.GenerateForFarmAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());
        return (new GenerateTenantHealthRemindersCommandHandler(farms.Object, reminders.Object, tenant.Object, clock.Object), reminders);
    }

    [Fact]
    public async Task TenantJob_WaitsFor7AmFarmTime()
    {
        var (early, earlyReminders) = TenantJob(localHour: 6);
        await early.Handle(new GenerateTenantHealthRemindersCommand(TenantGuid), CancellationToken.None);
        earlyReminders.Verify(r => r.GenerateForFarmAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);

        var (seven, reminders) = TenantJob(localHour: 7);
        await seven.Handle(new GenerateTenantHealthRemindersCommand(TenantGuid), CancellationToken.None);
        reminders.Verify(r => r.GenerateForFarmAsync(TenantGuid, It.IsAny<Guid>(), new DateOnly(2026, 11, 27), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TenantJob_SkipsTheDemoTenant()
    {
        var (handler, reminders) = TenantJob(localHour: 9, slug: Farm360.Application.Demo.DemoConstants.SlugPrefix + "abc");
        await handler.Handle(new GenerateTenantHealthRemindersCommand(TenantGuid), CancellationToken.None);
        reminders.Verify(r => r.GenerateForFarmAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
