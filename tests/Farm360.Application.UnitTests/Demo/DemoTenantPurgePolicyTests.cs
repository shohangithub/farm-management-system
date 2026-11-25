using Farm360.Application.Common.Interfaces;
using Farm360.Application.Demo;
using Farm360.Domain.Tenancy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Farm360.Application.UnitTests.Demo;

/// <summary>The purge may only ever pick RETIRED, OLDER demo tenants. Everything else is out of reach.</summary>
public sealed class DemoTenantPurgePolicyTests
{
    private static readonly DateTime Base = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DemoTenantCandidate Demo(int weeksAgo, TenantStatus status) =>
        new(Guid.NewGuid(), DemoConstants.SlugPrefix + Base.AddDays(-7 * weeksAgo).ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture),
            status, Base.AddDays(-7 * weeksAgo));

    [Fact]
    public void SelectsRetiredDemoTenantsOlderThanThePublishedOne()
    {
        var current = Demo(0, TenantStatus.Active);
        var old1 = Demo(1, TenantStatus.Cancelled);
        var old2 = Demo(2, TenantStatus.Cancelled);

        var selected = DemoTenantPurgePolicy.SelectPurgeable([current, old1, old2], current.Id);

        selected.Select(t => t.Id).Should().BeEquivalentTo([old1.Id, old2.Id]);
    }

    [Fact]
    public void NeverSelectsTheCurrentDemoTenant_EvenIfItWereCancelled()
    {
        var current = Demo(0, TenantStatus.Active);
        var old = Demo(1, TenantStatus.Cancelled);

        DemoTenantPurgePolicy.SelectPurgeable([current, old], current.Id)
            .Should().NotContain(t => t.Id == current.Id);

        // A published tenant that somehow got cancelled: refuse to purge anything at all.
        var cancelledCurrent = current with { Status = TenantStatus.Cancelled };
        DemoTenantPurgePolicy.SelectPurgeable([cancelledCurrent, old], current.Id).Should().BeEmpty();
    }

    [Fact]
    public void NeverSelectsTheNewestDemoTenant_EvenWhenItIsCancelledAndNotPublished()
    {
        var published = Demo(1, TenantStatus.Active);
        var newerDiscardedBuild = Demo(0, TenantStatus.Cancelled);
        var old = Demo(2, TenantStatus.Cancelled);

        var selected = DemoTenantPurgePolicy.SelectPurgeable([published, newerDiscardedBuild, old], published.Id);

        selected.Select(t => t.Id).Should().Equal(old.Id);
    }

    [Fact]
    public void NeverSelectsADemoTenantCreatedAfterThePublishedOne()
    {
        var published = Demo(3, TenantStatus.Active);
        var newerCancelled = Demo(2, TenantStatus.Cancelled);
        var newest = Demo(0, TenantStatus.Active);

        DemoTenantPurgePolicy.SelectPurgeable([published, newerCancelled, newest], published.Id).Should().BeEmpty();
    }

    [Theory]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.GracePeriod)]
    [InlineData(TenantStatus.Suspended)]
    public void NeverSelectsADemoTenantThatIsNotRetired(TenantStatus status)
    {
        var current = Demo(0, TenantStatus.Active);
        var notRetired = Demo(1, status);

        DemoTenantPurgePolicy.SelectPurgeable([current, notRetired], current.Id).Should().BeEmpty();
    }

    [Theory]
    [InlineData("green-valley-farm")]
    [InlineData("farm360-demo")]               // prefix without the dash and stamp
    [InlineData("farm360-demo-")]              // bare prefix, no stamp
    [InlineData("FARM360-DEMO-20260101000000")] // different case: not made by the demo code
    [InlineData("my-farm360-demo-20260101000000")]
    [InlineData("")]
    public void NeverSelectsANormalTenant_EvenWhenCancelledAndOld(string slug)
    {
        var current = Demo(0, TenantStatus.Active);
        var normal = new DemoTenantCandidate(Guid.NewGuid(), slug, TenantStatus.Cancelled, Base.AddYears(-2));

        var selected = DemoTenantPurgePolicy.SelectPurgeable([current, normal, Demo(1, TenantStatus.Cancelled)], current.Id);

        selected.Should().NotContain(t => t.Id == normal.Id);
        selected.Should().HaveCount(1);
    }

    [Fact]
    public void SelectsNothing_WhenNoDemoTenantIsPublished()
    {
        var old = Demo(1, TenantStatus.Cancelled);
        var older = Demo(2, TenantStatus.Cancelled);

        DemoTenantPurgePolicy.SelectPurgeable([old, older], null).Should().BeEmpty();
        DemoTenantPurgePolicy.SelectPurgeable([old, older], Guid.Empty).Should().BeEmpty();
    }

    [Fact]
    public void SelectsNothing_WhenThePublishedIdIsNotADemoTenant()
    {
        var normal = new DemoTenantCandidate(Guid.NewGuid(), "green-valley-farm", TenantStatus.Active, Base);
        var old = Demo(1, TenantStatus.Cancelled);

        DemoTenantPurgePolicy.SelectPurgeable([normal, old], normal.Id).Should().BeEmpty();
        DemoTenantPurgePolicy.SelectPurgeable([old], Guid.NewGuid()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("demo.owner.20261101000000@farm360.demo", true)]
    [InlineData("demo.viewer@farm360.demo", false)]
    [InlineData("demo.owner.@farm360.demo", false)]
    [InlineData("demo.owner.x@gmail.com", false)]
    [InlineData("+8801700000000", false)]
    [InlineData("DEMO.OWNER.x@farm360.demo", false)]
    [InlineData(null, false)]
    public void OnlyThrowawayOwnerUserNamesAreDemoOwners(string? userName, bool expected) =>
        DemoConstants.IsDemoOwnerUserName(userName).Should().Be(expected);

    [Fact]
    public void TheOwnerNameEachBuildCreatesIsRecognised() =>
        DemoConstants.IsDemoOwnerUserName(DemoConstants.OwnerUserName("20261101000000")).Should().BeTrue();

    [Fact]
    public async Task Purger_PurgesOnlyWhatThePolicySelects()
    {
        var current = Demo(0, TenantStatus.Active);
        var old = Demo(1, TenantStatus.Cancelled);
        var normal = new DemoTenantCandidate(Guid.NewGuid(), "green-valley-farm", TenantStatus.Cancelled, Base.AddYears(-1));

        var store = new Mock<IDemoTenantStore>();
        store.Setup(s => s.ListDemoTenantsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([current, old, normal]);
        store.Setup(s => s.GetPublishedTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current.Id);
        store.Setup(s => s.PurgeTenantAsync(old.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DemoTenantPurgeResult(old.Id, old.Slug, false, new Dictionary<string, long> { ["app.Animals"] = 3 }, ["/uploads/animals/a.jpg"]));
        var files = new Mock<IFileStorageService>();

        var summary = await new DemoTenantPurger(store.Object, files.Object, NullLogger<DemoTenantPurger>.Instance)
            .PurgeRetiredAsync(dryRun: false, CancellationToken.None);

        summary.Purged.Select(p => p.TenantId).Should().Equal(old.Id);
        store.Verify(s => s.PurgeTenantAsync(It.Is<Guid>(id => id != old.Id), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        files.Verify(f => f.DeleteFileAsync("/uploads/animals/a.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Purger_KeepsGoingAndReportsWhenOneTenantFails()
    {
        var current = Demo(0, TenantStatus.Active);
        var bad = Demo(1, TenantStatus.Cancelled);
        var good = Demo(2, TenantStatus.Cancelled);

        var store = new Mock<IDemoTenantStore>();
        store.Setup(s => s.ListDemoTenantsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([current, bad, good]);
        store.Setup(s => s.GetPublishedTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current.Id);
        store.Setup(s => s.PurgeTenantAsync(bad.Id, false, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        store.Setup(s => s.PurgeTenantAsync(good.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DemoTenantPurgeResult(good.Id, good.Slug, false, new Dictionary<string, long>(), []));
        var files = new Mock<IFileStorageService>();

        var summary = await new DemoTenantPurger(store.Object, files.Object, NullLogger<DemoTenantPurger>.Instance)
            .PurgeRetiredAsync(dryRun: false, CancellationToken.None);

        summary.Failed.Select(f => f.TenantId).Should().Equal(bad.Id);
        summary.Purged.Select(p => p.TenantId).Should().Equal(good.Id);
    }

    [Fact]
    public async Task Purger_DryRun_DeletesNoFiles()
    {
        var current = Demo(0, TenantStatus.Active);
        var old = Demo(1, TenantStatus.Cancelled);

        var store = new Mock<IDemoTenantStore>();
        store.Setup(s => s.ListDemoTenantsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([current, old]);
        store.Setup(s => s.GetPublishedTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(current.Id);
        store.Setup(s => s.PurgeTenantAsync(old.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DemoTenantPurgeResult(old.Id, old.Slug, true, new Dictionary<string, long>(), ["/uploads/x.png"]));
        var files = new Mock<IFileStorageService>();

        await new DemoTenantPurger(store.Object, files.Object, NullLogger<DemoTenantPurger>.Instance)
            .PurgeRetiredAsync(dryRun: true, CancellationToken.None);

        files.Verify(f => f.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
