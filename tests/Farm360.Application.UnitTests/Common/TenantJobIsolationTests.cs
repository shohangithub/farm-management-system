using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Jobs;
using Farm360.Application.Feeding.Jobs;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Common;

public class TenantJobIsolationTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by the DI container.")]
    private sealed class FakeTenantService : ITenantService
    {
        public Guid TenantId { get; private set; }
        public string TenantSlug { get; private set; } = string.Empty;
        public string TenantName { get; private set; } = string.Empty;
        public string SubscriptionTier { get; private set; } = string.Empty;
        public string TenantStatus { get; private set; } = string.Empty;
        public bool IsActive => true;
        public bool IsGracePeriod => false;

        public void SetTenant(Guid tenantId, string slug, string name, string tier, string status)
        {
            TenantId = tenantId; TenantSlug = slug; TenantName = name; SubscriptionTier = tier; TenantStatus = status;
        }
    }

    private static (TenantJobRunner Runner, Tenant A, Tenant B) RunnerWithTwoTenants()
    {
        var a = Tenant.Create("Alpha Farms", "alpha", SubscriptionTier.Starter);
        var b = Tenant.Create("Beta Farms", "beta", SubscriptionTier.Starter);
        var repo = Substitute.For<ITenantRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Tenant> { a, b });

        var services = new ServiceCollection();
        services.AddSingleton(repo);
        services.AddScoped<ITenantService, FakeTenantService>();
        services.AddScoped(_ => Substitute.For<ISender>());
        var provider = services.BuildServiceProvider();

        return (new TenantJobRunner(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<TenantJobRunner>.Instance), a, b);
    }

    [Fact]
    public async Task OneTenantThrowing_DoesNotStopTheOthers()
    {
        var (runner, a, b) = RunnerWithTwoTenants();
        var processed = new List<Guid>();

        var summary = await runner.ForEachTenantAsync("test", (scope, _) =>
        {
            // The ambient tenant is set on each tenant's own scope.
            scope.Services.GetRequiredService<ITenantService>().TenantId.Should().Be(scope.TenantId);
            if (scope.TenantId == a.Id)
                throw new InvalidOperationException("bad plan");
            processed.Add(scope.TenantId);
            return Task.CompletedTask;
        });

        processed.Should().Equal(b.Id);
        summary.Succeeded.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.FailedTenantIds.Should().Equal(a.Id);
    }

    [Fact]
    public async Task EachTenant_GetsItsOwnScope()
    {
        var (runner, _, _) = RunnerWithTwoTenants();
        var tenantServices = new List<ITenantService>();

        await runner.ForEachTenantAsync("test", (scope, _) =>
        {
            tenantServices.Add(scope.Services.GetRequiredService<ITenantService>());
            return Task.CompletedTask;
        });

        tenantServices.Should().HaveCount(2);
        tenantServices[0].Should().NotBeSameAs(tenantServices[1]);
    }

    [Fact]
    public async Task TenantDailyFeeding_OneFarmFailing_StillGeneratesTheOtherFarm()
    {
        var tenantId = Guid.NewGuid();
        var badFarm = Guid.NewGuid();
        var goodFarm = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 17);

        var plans = Substitute.For<IAnimalFeedingPlanRepository>();
        plans.GetActivePlansAsync(tenantId, Arg.Any<CancellationToken>()).Returns(new List<AnimalFeedingPlan>
        {
            new(Guid.NewGuid(), tenantId, badFarm, Guid.NewGuid(), FeedingPlanType.FixedQuantity, today.AddDays(-3), null, animalId: Guid.NewGuid()),
            new(Guid.NewGuid(), tenantId, goodFarm, Guid.NewGuid(), FeedingPlanType.FixedQuantity, today.AddDays(-3), null, animalId: Guid.NewGuid()),
        });
        var entries = Substitute.For<IDailyFeedingEntryRepository>();
        entries.GetLastGeneratedEntryDateAsync(tenantId, Arg.Any<CancellationToken>()).Returns(today.AddDays(-1));
        var clock = Substitute.For<IFarmClock>();
        clock.TodayFor(tenantId).Returns(today);

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Is<GenerateFarmDailyFeedingEntriesCommand>(c => c.FarmId == badFarm), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("broken rule set"));
        sender.Send(Arg.Is<GenerateFarmDailyFeedingEntriesCommand>(c => c.FarmId == goodFarm), Arg.Any<CancellationToken>())
            .Returns(3);

        var handler = new GenerateTenantDailyFeedingEntriesCommandHandler(plans, entries, clock, sender,
            NullLogger<GenerateTenantDailyFeedingEntriesCommandHandler>.Instance);

        var result = await handler.Handle(new GenerateTenantDailyFeedingEntriesCommand(tenantId), CancellationToken.None);

        result.EntriesCreated.Should().Be(3);
        result.FarmDaysFailed.Should().Be(1);
        result.Days.Should().Equal(today);
    }

    [Fact]
    public async Task TenantDailyFeeding_CatchesUpMissedDays_CappedAtSeven()
    {
        var tenantId = Guid.NewGuid();
        var farm = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 17);

        var plans = Substitute.For<IAnimalFeedingPlanRepository>();
        plans.GetActivePlansAsync(tenantId, Arg.Any<CancellationToken>()).Returns(new List<AnimalFeedingPlan>
        {
            new(Guid.NewGuid(), tenantId, farm, Guid.NewGuid(), FeedingPlanType.FixedQuantity, today.AddDays(-60), null, animalId: Guid.NewGuid()),
        });
        var entries = Substitute.For<IDailyFeedingEntryRepository>();
        var clock = Substitute.For<IFarmClock>();
        clock.TodayFor(tenantId).Returns(today);
        var sender = Substitute.For<ISender>();
        var handler = new GenerateTenantDailyFeedingEntriesCommandHandler(plans, entries, clock, sender,
            NullLogger<GenerateTenantDailyFeedingEntriesCommandHandler>.Instance);

        // Last run three days ago: the two missed days and today.
        entries.GetLastGeneratedEntryDateAsync(tenantId, Arg.Any<CancellationToken>()).Returns(today.AddDays(-3));
        (await handler.Handle(new GenerateTenantDailyFeedingEntriesCommand(tenantId), CancellationToken.None))
            .Days.Should().Equal(today.AddDays(-2), today.AddDays(-1), today);

        // Last run a month ago: only the last seven days.
        entries.GetLastGeneratedEntryDateAsync(tenantId, Arg.Any<CancellationToken>()).Returns(today.AddDays(-30));
        (await handler.Handle(new GenerateTenantDailyFeedingEntriesCommand(tenantId), CancellationToken.None))
            .Days.Should().HaveCount(7).And.EndWith(today);
    }
}
