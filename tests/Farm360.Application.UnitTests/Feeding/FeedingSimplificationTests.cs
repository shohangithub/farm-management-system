using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.FeedingSchedules;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>Group plans, pauses and schedule conversion behind the simplified feeding screens.</summary>
public class FeedingSimplificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    [Fact]
    public void GroupEntry_CostCoversEveryHead()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, Guid.NewGuid(), FarmId, new DateOnly(2026, 10, 15),
            Guid.NewGuid(), expectedKg: 2m, ruleLineId: null, shedId: Guid.NewGuid());

        entry.SetConsumptionCost(50m, headCount: 10);

        entry.TotalCostBdt.Should().Be(1_000m, "2 kg × 10 head × ৳50");
        entry.UnitCostAtConsumptionBdt.Should().Be(50m);
    }

    [Fact]
    public void IndividualEntry_CostIsOneHead()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, Guid.NewGuid(), FarmId, new DateOnly(2026, 10, 15),
            Guid.NewGuid(), expectedKg: 2m, ruleLineId: null);

        entry.SetConsumptionCost(50m);

        entry.TotalCostBdt.Should().Be(100m);
    }

    [Fact]
    public void Pause_CoversEveryDayUntilFeedingResumes()
    {
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            new DateOnly(2026, 10, 1), null, animalId: Guid.NewGuid());
        plan.AddExclusion(new DateOnly(2026, 10, 15), "Sick", new DateOnly(2026, 10, 18));
        plan.AddExclusion(new DateOnly(2026, 10, 25), "Market day");

        plan.IsPausedOn(new DateOnly(2026, 10, 14)).Should().BeFalse();
        plan.IsPausedOn(new DateOnly(2026, 10, 15)).Should().BeTrue();
        plan.IsPausedOn(new DateOnly(2026, 10, 17)).Should().BeTrue("a pause used to cover only its first day");
        plan.IsPausedOn(new DateOnly(2026, 10, 18)).Should().BeFalse("feeding resumes on this day");
        plan.IsPausedOn(new DateOnly(2026, 10, 25)).Should().BeTrue("a single-day exclusion");
        plan.IsPausedOn(new DateOnly(2026, 10, 26)).Should().BeFalse();
        plan.PausedUntil(new DateOnly(2026, 10, 16)).Should().Be(new DateOnly(2026, 10, 18));
    }

    [Fact]
    public async Task ConvertSchedules_MakesGroupPlans_AndSwitchesTheSchedulesOff()
    {
        var shedId = Guid.NewGuid();
        var formulaId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var linked = new FeedingSchedule(Guid.NewGuid(), TenantId, FarmId, formulaId, "Shed A morning", 3m,
            ScheduleFrequency.OnceDaily, today.AddDays(-30), shedId: shedId);
        var unlinked = new FeedingSchedule(Guid.NewGuid(), TenantId, FarmId, formulaId, "Everyone", 2m,
            ScheduleFrequency.OnceDaily, today.AddDays(-30));
        var ended = new FeedingSchedule(Guid.NewGuid(), TenantId, FarmId, formulaId, "Old", 2m,
            ScheduleFrequency.OnceDaily, today.AddDays(-60), shedId: shedId, endDate: today.AddDays(-1));

        var schedules = Substitute.For<IFeedingScheduleRepository>();
        schedules.GetListByFarmAsync(TenantId, FarmId, Arg.Any<CancellationToken>()).Returns(new List<FeedingSchedule> { linked, unlinked, ended });
        var plans = Substitute.For<IAnimalFeedingPlanRepository>();
        plans.GetActivePlansByFarmAsync(TenantId, FarmId, Arg.Any<CancellationToken>()).Returns(new List<AnimalFeedingPlan>());
        var added = new List<AnimalFeedingPlan>();
        await plans.AddAsync(Arg.Do<AnimalFeedingPlan>(added.Add), Arg.Any<CancellationToken>());

        var direct = Substitute.For<IDirectFeedService>();
        var ruleSet = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Direct · Starter · 3 kg/day", TargetAnimalType.Cattle,
            FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
        direct.GetOrCreateDirectRuleSetAsync(Arg.Any<DirectRation>(), Arg.Any<TargetAnimalType>(), Arg.Any<CancellationToken>()).Returns(ruleSet);
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);

        var result = await new ConvertFeedingSchedulesCommandHandler(schedules, plans, direct, tenant, Substitute.For<IUnitOfWork>())
            .Handle(new ConvertFeedingSchedulesCommand(FarmId), CancellationToken.None);

        result.Converted.Should().Be(1);
        result.Skipped.Should().Be(2);
        added.Should().ContainSingle();
        added[0].ShedId.Should().Be(shedId);
        added[0].AnimalId.Should().BeNull("it feeds everyone in the shed");
        added[0].StartDate.Should().Be(today, "earlier days are not back-filled");
        await direct.Received(1).GetOrCreateDirectRuleSetAsync(
            Arg.Is<DirectRation>(r => r.FormulaId == formulaId && r.KgPerDay == 3m), Arg.Any<TargetAnimalType>(), Arg.Any<CancellationToken>());
        linked.IsActive.Should().BeFalse();
        ended.IsActive.Should().BeFalse();
        unlinked.IsActive.Should().BeTrue("it has no shed, pen or batch to feed, so it is left for the user");
    }
}
