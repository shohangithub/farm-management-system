using Farm360.Application.Health.Programmes;
using Farm360.Application.InAppNotifications;
using Farm360.Application.IntegrationTests.Infrastructure;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm360.Application.IntegrationTests.Persistence;

/// <summary>
/// The health-schedule / notification migration applies (the fixture migrates a fresh database with
/// every migration), stored reminders round-trip with their keys and parameters, the per-user daily
/// key is unique, and protocol steps keep their kind, repeat and weight-based dose.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class HealthScheduleNotificationTests(SqlServerFixture fixture)
{
    private static T Create<T>(string typeName, Farm360.Persistence.Context.ApplicationDbContext db) =>
        (T)Activator.CreateInstance(typeof(Farm360.Persistence.Context.ApplicationDbContext).Assembly.GetType(typeName, throwOnError: true)!, db)!;

    /// <summary>The task list, reminder recipients and bell queries translate and run on SQL Server.</summary>
    [SqlServerFact]
    public async Task TaskAndNotificationQueriesRunOnSqlServer()
    {
        var tenantId = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = fixture.CreateContext(TestTenantService.For(tenantId));

        var tasks = Create<Farm360.Application.Health.Tasks.IHealthTaskSource>("Farm360.Persistence.Queries.HealthTaskSource", db);
        (await tasks.GetPendingAsync(Guid.NewGuid(), new DateOnly(2026, 11, 28))).Should().BeEmpty();
        (await tasks.GetStockItemsAsync([Guid.NewGuid()])).Should().BeEmpty();
        (await tasks.GetUnitsAsync([Guid.NewGuid()])).Should().BeEmpty();

        var recipients = Create<IHealthReminderRecipients>("Farm360.Persistence.Queries.HealthReminderRecipients", db);
        (await recipients.GetUserIdsAsync(tenantId, Guid.NewGuid())).Should().BeEmpty("an unknown farm has nobody to remind");

        var store = Create<INotificationStore>("Farm360.Persistence.Queries.NotificationStore", db);
        store.Add(Notification.CreateReminder(tenantId, user, null, NotificationType.Info, "health.today", "Health tasks for today",
            "{count} animals are due today: {items}", null, null, "health:today:x:1"));
        await db.SaveChangesAsync();

        (await store.GetExistingKeysAsync([user], ["health:today:x:1", "health:today:x:2"]))
            .Should().BeEquivalentTo(new[] { (user, "health:today:x:1") });
        (await store.CountUnreadAsync(user)).Should().Be(1);
        (await store.GetForUserAsync(user, 10)).Should().ContainSingle();
        (await store.MarkAllReadAsync(user)).Should().Be(1);
        (await store.CountUnreadAsync(user)).Should().Be(0);
    }

    [SqlServerFact]
    public async Task MigrationIsApplied()
    {
        await using var db = fixture.CreateContext(TestTenantService.For(Guid.NewGuid()));
        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(m => m.EndsWith("_AddHealthScheduleRemindersAndNotifications", StringComparison.Ordinal));
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [SqlServerFact]
    public async Task RemindersSaveAndReadBackPerUserWithKeysAndParameters()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var key = $"health:today:{farmId:N}:20261127";

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.Notifications.AddRange(
                Notification.CreateReminder(tenantId, user, farmId, NotificationType.Info, "health.today",
                    "Health tasks for today", "{count} animals are due today: {items}",
                    """{"count":"3","items":"Dewormer (B-04, B-07), FMD (C-02)"}""", "/health/today?focus=today", key),
                Notification.CreateReminder(tenantId, other, farmId, NotificationType.Warning, "health.overdue",
                    "Overdue health tasks", "{count} animal has overdue tasks: {items}", """{"count":"1","items":"LSD (A-01)"}""",
                    "/health/today?focus=overdue", $"health:overdue:{farmId:N}:20261127"));
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            var mine = await read.Notifications.AsNoTracking().Where(n => n.UserId == user).ToListAsync();
            mine.Should().ContainSingle();
            var dto = NotificationDto.From(mine[0]);
            dto.Title.Should().Be("Health tasks for today");
            dto.Params["count"].Should().Be("3");
            dto.Params["items"].Should().Be("Dewormer (B-04, B-07), FMD (C-02)");
            dto.Link.Should().Be("/health/today?focus=today");
            dto.Category.Should().Be("health.today");
            dto.FarmId.Should().Be(farmId);
            dto.IsRead.Should().BeFalse();

            var tracked = await read.Notifications.SingleAsync(n => n.UserId == user);
            tracked.MarkAsRead();
            await read.SaveChangesAsync();
        }

        await using (var check = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            (await check.Notifications.CountAsync(n => n.UserId == user && !n.IsRead)).Should().Be(0);
            (await check.Notifications.CountAsync(n => n.UserId == other && !n.IsRead)).Should().Be(1);
        }

        // Another tenant sees none of them.
        await using (var foreign = fixture.CreateContext(TestTenantService.For(Guid.NewGuid())))
        {
            (await foreign.Notifications.CountAsync()).Should().Be(0);
        }

        // The same daily reminder cannot be stored twice for a user (the job's safety net).
        await using (var dup = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            dup.Notifications.Add(Notification.CreateReminder(tenantId, user, farmId, NotificationType.Info, "health.today",
                "Health tasks for today", "{count} animals are due today: {items}", null, null, key));
            var save = () => dup.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [SqlServerFact]
    public async Task ProtocolStepsKeepKindRepeatAndWeightDose()
    {
        var tenantId = Guid.NewGuid();
        var protocol = MarketCattleProgrammeTemplate.Build(tenantId, repeatEveryDays: 90);

        await using (var db = fixture.CreateContext(TestTenantService.For(tenantId)))
        {
            db.VaccinationProtocols.Add(protocol);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(TestTenantService.For(tenantId));
        var stored = await read.VaccinationProtocols.Include(p => p.Steps).AsNoTracking().SingleAsync(p => p.Id == protocol.Id);
        stored.AutoStartForPurchased.Should().BeTrue();
        stored.Title.Should().Be(MarketCattleProgrammeTemplate.Title);
        stored.Steps.Should().HaveCount(protocol.Steps.Count);
        stored.Steps.Count(s => s.Kind == HealthTaskKind.CareTask).Should().Be(protocol.Steps.Count(s => s.Kind == HealthTaskKind.CareTask));
        stored.Steps.Where(s => s.RepeatEveryDays == 90).Select(s => s.TargetAgeDays).Should().BeEquivalentTo([93, 107]);

        var fastVet = stored.Steps.Single(s => s.VaccineName == "Fast Vet");
        fastVet.DoseRule.Should().NotBeNull();
        fastVet.DoseRule!.DoseFor(290m).Should().Be(2m);
        fastVet.DoseRounding.Should().Be(DoseRounding.Half);
        fastVet.DoseUnit.Should().Be("tablet");
        stored.Steps.Single(s => s.ActionLink == "sell-or-keep").TargetAgeDays.Should().Be(89);
    }
}
