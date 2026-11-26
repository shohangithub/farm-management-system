using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Queries;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Queries.Analytics;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

public class GetFcrAnalyticsQueryTests
{
    [Fact]
    public async Task Counts_confirmed_daily_feeding_not_only_old_shed_logs()
    {
        var tenant = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 26);

        var logs = Substitute.For<IFeedConsumptionLogRepository>();
        logs.GetLogsAsync(tenant, farm, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<FeedConsumptionLog>());

        var allocations = Substitute.For<IAnimalFeedAllocationRepository>();
        allocations.GetDailyEatenByFarmAsync(farm, today.AddDays(-365), today, Arg.Any<CancellationToken>())
            .Returns(new List<AnimalFeedDay>
            {
                new(Guid.NewGuid(), new DateOnly(2026, 11, 1), 60m, 1_800m),
                new(Guid.NewGuid(), new DateOnly(2026, 11, 2), 40m, 1_200m),
            });

        var analytics = Substitute.For<IAnalyticsQueryService>();
        analytics.GetTotalWeightGainKgAsync(farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(10m);

        var tenantService = Substitute.For<ITenantService>();
        tenantService.TenantId.Returns(tenant);
        var clock = Substitute.For<IFarmClock>();
        clock.TodayForFarm(tenant, farm).Returns(today);

        var handler = new GetFcrAnalyticsQueryHandler(logs, allocations, analytics, tenantService, clock);
        var result = await handler.Handle(new GetFcrAnalyticsQuery(farm), CancellationToken.None);

        result.TotalFeedConsumedKg.Should().Be(100m);
        result.TotalFeedCostBdt.Should().Be(3_000m);
        result.FcrValue.Should().Be(10m);
        result.CostPerKgGainBdt.Should().Be(300m);
        result.MonthlyTrends.Should().ContainSingle();
    }
}
