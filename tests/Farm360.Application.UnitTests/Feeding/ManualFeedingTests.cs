using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

public class ManualFeedingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

    private sealed class Fixture
    {
        public IAnimalRepository Animals { get; } = Substitute.For<IAnimalRepository>();
        public IAnimalFeedingPlanRepository Plans { get; } = Substitute.For<IAnimalFeedingPlanRepository>();
        public IDailyFeedingEntryRepository Entries { get; } = Substitute.For<IDailyFeedingEntryRepository>();
        public IDirectFeedService Direct { get; } = Substitute.For<IDirectFeedService>();
        public ISender Sender { get; } = Substitute.For<ISender>();
        public List<AnimalFeedingPlan> AddedPlans { get; } = [];
        public List<DailyFeedingEntry> AddedEntries { get; } = [];
        public FeedFormula Formula { get; } = new(Guid.NewGuid(), TenantId, "CP Starter", TargetAnimalType.Cattle);

        public Fixture()
        {
            var ruleSet = new FeedingRuleSet(Guid.NewGuid(), TenantId, "Direct · CP Starter · 2 kg/day", TargetAnimalType.Cattle,
                FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
            ruleSet.AddRuleLine(null, null, null, null, FeedCategory.Concentrate, 2m, Formula.Id);
            Direct.GetOrCreateDirectRuleSetAsync(Arg.Any<DirectRation>(), Arg.Any<TargetAnimalType>(), Arg.Any<CancellationToken>()).Returns(ruleSet);
            Plans.AddAsync(Arg.Do<AnimalFeedingPlan>(AddedPlans.Add), Arg.Any<CancellationToken>());
            Entries.AddAsync(Arg.Do<DailyFeedingEntry>(AddedEntries.Add), Arg.Any<CancellationToken>());
        }

        public Animal AddAnimal(string tag, Guid? farmId = null)
        {
            var a = Animal.Create(TenantId, farmId ?? FarmId, AnimalTag.Create(tag, TagType.Manual), AnimalSpecies.CattleBeef,
                Guid.NewGuid(), AnimalSex.Male, Day.AddYears(-1), AcquisitionType.Purchased, Day.AddMonths(-1), 50_000m, null);
            Animals.GetByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
            return a;
        }

        public RecordManualFeedingCommandHandler Handler()
        {
            var formulas = Substitute.For<IFeedFormulaRepository>();
            formulas.GetByIdAsync(Formula.Id, Arg.Any<CancellationToken>()).Returns(Formula);
            var allocation = Substitute.For<IFeedAllocationService>();
            allocation.BuildAllocationsAsync(Arg.Any<DailyFeedingEntry>(), Arg.Any<AnimalFeedingPlan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new List<AnimalFeedAllocation>());
            var tenant = Substitute.For<ITenantService>();
            tenant.TenantId.Returns(TenantId);
            return new RecordManualFeedingCommandHandler(Animals, Plans, Entries, Substitute.For<IAnimalFeedAllocationRepository>(),
                allocation, formulas, Direct, tenant, Substitute.For<IUnitOfWork>(), Sender);
        }
    }

    [Fact]
    public async Task ManualFeed_MakesAClosedOneDayPlanAndAConfirmedEntryPerAnimal()
    {
        var f = new Fixture();
        var a1 = f.AddAnimal("A1");
        var a2 = f.AddAnimal("A2");

        var result = await f.Handler().Handle(
            new RecordManualFeedingCommand(FarmId, Day, [a1.Id, a2.Id, a1.Id], f.Formula.Id, null, 1.5m, "missed morning"),
            CancellationToken.None);

        result.Recorded.Should().Be(2, "a repeated animal counts once");
        result.Notes.Should().BeEmpty();
        f.AddedPlans.Should().HaveCount(2).And.OnlyContain(p =>
            p.StartDate == Day && p.EndDate == Day && p.Status == FeedingPlanStatus.Completed, "the daily job must never feed it again");
        f.AddedEntries.Should().HaveCount(2).And.OnlyContain(e => e.EntryDate == Day && e.ExpectedKg == 1.5m && e.FormulaId == f.Formula.Id);
        await f.Sender.Received(2).Send(
            Arg.Is<ConfirmDailyFeedingEntryCommand>(c => c.ActualKg == 1.5m && c.AdjustmentReason == "Manual entry: missed morning"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManualFeed_SkipsAnimalsOfOtherFarms_AndKeepsShortStockEntriesPending()
    {
        var f = new Fixture();
        var here = f.AddAnimal("A1");
        var elsewhere = f.AddAnimal("B1", Guid.NewGuid());
        f.Sender.Send(Arg.Any<ConfirmDailyFeedingEntryCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Insufficient stock for 'Maize'."));

        var result = await f.Handler().Handle(
            new RecordManualFeedingCommand(FarmId, Day, [here.Id, elsewhere.Id], f.Formula.Id, null, 2m), CancellationToken.None);

        result.Recorded.Should().Be(0);
        f.AddedEntries.Should().ContainSingle("the entry is kept, to confirm after a stock-in");
        result.Notes.Should().HaveCount(2)
            .And.Contain(n => n.Contains("not found on this farm"))
            .And.Contain(n => n.Contains("Insufficient stock"));
    }

    [Fact]
    public void Validator_RefusesFutureDaysAndMissingFeed()
    {
        var v = new RecordManualFeedingCommandValidator();
        v.Validate(new RecordManualFeedingCommand(FarmId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), [Guid.NewGuid()], Guid.NewGuid(), null, 1m))
            .IsValid.Should().BeFalse();
        v.Validate(new RecordManualFeedingCommand(FarmId, Day, [Guid.NewGuid()], null, null, 1m)).IsValid.Should().BeFalse();
        v.Validate(new RecordManualFeedingCommand(FarmId, Day, [Guid.NewGuid()], null, Guid.NewGuid(), 0.2m)).IsValid.Should().BeTrue();
    }
}
