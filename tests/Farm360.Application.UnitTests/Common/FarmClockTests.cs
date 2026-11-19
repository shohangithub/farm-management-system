using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Time;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Common;

public class FarmClockTests
{
    // 2026-11-16 20:00 UTC = 2026-11-17 02:00 in Dhaka (UTC+6).
    private static readonly DateTime TwoAmDhakaUtc = new(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);

    private static (FarmClock Clock, IFarmTimeZoneSource Source, ITenantService Tenant) Create(Guid tenantId, params string?[] zones)
    {
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(tenantId);
        var source = Substitute.For<IFarmTimeZoneSource>();
        source.GetZoneCandidates(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(zones);
        var time = Substitute.For<IDateTimeService>();
        time.UtcNow.Returns(TwoAmDhakaUtc);
        return (new FarmClock(tenant, source, time, new FarmTimeZoneCache()), source, tenant);
    }

    [Fact]
    public void Today_At2amDhaka_IsTheNextDayComparedToUtc()
    {
        var (clock, _, _) = Create(Guid.NewGuid(), "Asia/Dhaka");

        DateOnly.FromDateTime(TwoAmDhakaUtc).Should().Be(new DateOnly(2026, 11, 16));
        clock.Today.Should().Be(new DateOnly(2026, 11, 17));
        clock.Now.Should().Be(new DateTime(2026, 11, 17, 2, 0, 0));
    }

    [Fact]
    public void WindowsZoneId_ResolvesTheSame()
    {
        var (clock, _, _) = Create(Guid.NewGuid(), "Bangladesh Standard Time");

        clock.Today.Should().Be(new DateOnly(2026, 11, 17));
    }

    [Fact]
    public void UnknownOrMissingZone_FallsBackToBangladesh()
    {
        var (clock, _, _) = Create(Guid.NewGuid(), "Not/AZone", null, "");

        clock.Today.Should().Be(new DateOnly(2026, 11, 17));
        clock.TimeZone.GetUtcOffset(TwoAmDhakaUtc).Should().Be(TimeSpan.FromHours(6));
    }

    [Fact]
    public void FirstResolvableCandidate_Wins()
    {
        // UTC zone: still the 16th at 20:00 UTC.
        var (clock, _, _) = Create(Guid.NewGuid(), "UTC", "Asia/Dhaka");

        clock.Today.Should().Be(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public void TodayFor_ResolvesPerTenant_ForJobsWithoutARequestTenant()
    {
        var (clock, source, _) = Create(Guid.Empty, "Asia/Dhaka");
        var tenantId = Guid.NewGuid();

        clock.TodayFor(tenantId).Should().Be(new DateOnly(2026, 11, 17));
        source.Received(1).GetZoneCandidates(tenantId, null);
    }

    [Fact]
    public void ZoneLookups_AreCached()
    {
        var tenantId = Guid.NewGuid();
        var (clock, source, _) = Create(tenantId, "Asia/Dhaka");

        _ = clock.Today;
        _ = clock.Now;
        _ = clock.TodayFor(tenantId);

        source.Received(1).GetZoneCandidates(tenantId, null);
    }

    [Fact]
    public void SystemClock_UsesBangladesh()
    {
        FarmClock.System.TimeZone.GetUtcOffset(TwoAmDhakaUtc).Should().Be(TimeSpan.FromHours(6));
    }
}
