using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Feeding;

/// <summary>The "fed on day D" farm rule and the domain steps of applying an animal's exit to feeding.</summary>
public class AnimalExitFeedingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly Acquired = new(2026, 1, 10);
    private static readonly DateOnly Exit = new(2026, 11, 13);

    private static Animal NewAnimal() => Animal.Create(
        TenantId, FarmId, AnimalTag.Create("C-1001", TagType.EarTag),
        AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1),
        AcquisitionType.Purchased, Acquired, 50_000m, null);

    private static bool FedByQuery(Animal animal, DateOnly day) => AnimalFeedingPresence.FedOn(day).Compile()(animal);

    [Fact]
    public void ActiveAnimal_IsFedFromItsAcquisitionDate()
    {
        var animal = NewAnimal();

        AnimalFeedingPresence.IsFedOn(animal, Acquired.AddDays(-1)).Should().BeFalse();
        AnimalFeedingPresence.IsFedOn(animal, Acquired).Should().BeTrue();
        FedByQuery(animal, Acquired.AddDays(-1)).Should().BeFalse();
        FedByQuery(animal, Acquired).Should().BeTrue();
    }

    [Fact]
    public void QuarantinedAnimal_IsOnTheFarmAndFed()
    {
        var animal = NewAnimal();
        animal.Quarantine("Observation");

        AnimalFeedingPresence.IsFedOn(animal, Exit).Should().BeTrue();
        FedByQuery(animal, Exit).Should().BeTrue();
    }

    [Fact]
    public void SoldAnimal_IsFedBeforeTheSaleDay_NotOnItOrAfter()
    {
        var animal = NewAnimal();
        animal.Sell(80_000m, Exit, Guid.NewGuid(), "Buyer", null);

        foreach (var (day, fed) in new[] { (Exit.AddDays(-1), true), (Exit, false), (Exit.AddDays(2), false) })
        {
            AnimalFeedingPresence.IsFedOn(animal, day).Should().Be(fed, $"day {day}");
            FedByQuery(animal, day).Should().Be(fed, $"query, day {day}");
        }

        AnimalFeedingPresence.NotFedReason(animal, Exit.AddDays(2)).Should().Contain("was sold on 13 Nov 2026");
    }

    [Fact]
    public void DeadAnimal_IsFedBeforeTheDeathDay_NotOnIt()
    {
        var animal = NewAnimal();
        animal.RecordDeath(DisposalReason.Disease, Exit, null);

        AnimalFeedingPresence.IsFedOn(animal, Exit.AddDays(-1)).Should().BeTrue();
        AnimalFeedingPresence.IsFedOn(animal, Exit).Should().BeFalse();
        FedByQuery(animal, Exit).Should().BeFalse();
        AnimalFeedingPresence.NotFedReason(animal, Exit).Should().Contain("died on 13 Nov 2026");
    }

    [Fact]
    public void PlanEndsOnExit_OnceOnly()
    {
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            Acquired, null, animalId: Guid.NewGuid());

        plan.EndOnExit(Exit).Should().BeTrue();
        plan.Status.Should().Be(FeedingPlanStatus.Cancelled);
        plan.EndDate.Should().Be(Exit);
        plan.EndOnExit(Exit).Should().BeFalse("a second run changes nothing");
    }

    [Fact]
    public void ConfirmedEntry_VoidedAfterExit_IsSkippedWithNothingFedAndNoCost()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, Guid.NewGuid(), FarmId, Exit.AddDays(1), Guid.NewGuid(), 5m);
        entry.SetConsumptionCost(40m);
        entry.Confirm(5m);

        entry.VoidAfterExit("Animal sold on 13 Nov 2026").Should().BeTrue();

        entry.Status.Should().Be(DailyFeedingEntryStatus.Skipped);
        entry.ActualKg.Should().Be(0m);
        entry.TotalCostBdt.Should().Be(0m);
        entry.AdjustmentReason.Should().StartWith("Animal sold on 13 Nov 2026");
        entry.VoidAfterExit("again").Should().BeFalse();
    }

    [Fact]
    public void ReturnedFeedStock_ComesBackAtItsIssueCost_LeavingAverageCostUndistorted()
    {
        var item = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, "Concentrate", "CON-1", InventoryCategory.Feed, "kg", 0m, 100m, 40m);
        item.DeductStock(10m, Guid.NewGuid());

        item.ReturnConsumedStock(10m, 40m);

        item.CurrentStock.Should().Be(100m);
        item.WeightedAverageCostBdt.Should().Be(40m, "returning what was issued at 40 must not move a 40 average");
    }

    [Fact]
    public void ReturnedFeedStock_AfterAPriceChange_BlendsAtTheIssueCost()
    {
        var item = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, "Concentrate", "CON-1", InventoryCategory.Feed, "kg", 0m, 10m, 40m);
        item.DeductStock(10m, Guid.NewGuid());          // issued at 40
        item.ReceiveStock(10m, 60m, Guid.NewGuid());   // new stock at 60

        item.ReturnConsumedStock(10m, 40m);

        item.CurrentStock.Should().Be(20m);
        item.WeightedAverageCostBdt.Should().Be(50m, "(10 x 60 + 10 x 40) / 20");
    }

    [Fact]
    public void GroupShare_Reallocated_CarriesTheNewHeadCount()
    {
        var row = AnimalFeedAllocation.Create(TenantId, Guid.NewGuid(), FarmId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Exit, 3m, 120m, 40m, FeedAllocationOrigin.GroupPlanShare, Farm360.Domain.BusinessRules.FeedAllocationMethod.EqualPerHead,
            200m, 1m / 3m, 3);

        row.Reallocate(4.5m, 180m, 40m, 200m, 0.5m, 2, Farm360.Domain.BusinessRules.FeedAllocationMethod.EqualPerHead);

        row.HeadCountAtAllocation.Should().Be(2);
        row.AllocatedCostBdt.Should().Be(180m);
    }

    [Fact]
    public void FeedConsumptionReversal_IsANewStockMovementType()
    {
        Enum.IsDefined(StockTransactionType.FeedConsumptionReversal).Should().BeTrue();
    }

    [Fact]
    public void UndoneExit_ReopensThePlanItEnded_AndItsSkippedEntries()
    {
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            Acquired, null, animalId: Guid.NewGuid());
        plan.EndOnExit(Exit);

        plan.ReopenAfterExitUndone(Exit).Should().BeTrue();
        plan.Status.Should().Be(FeedingPlanStatus.Active);
        plan.EndDate.Should().BeNull();

        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, Guid.NewGuid(), FarmId, Exit.AddDays(1), Guid.NewGuid(), 5m);
        entry.Confirm(5m);
        entry.VoidAfterExit("Animal sold on 13 Nov 2026");

        entry.ReopenAfterExitUndone("Animal sold on 13 Nov 2026").Should().BeTrue();
        entry.Status.Should().Be(DailyFeedingEntryStatus.Pending);
        entry.ReopenAfterExitUndone("Animal sold on 13 Nov 2026").Should().BeFalse("a second run changes nothing");
    }

    [Fact]
    public void UndoneExit_LeavesAPlanStoppedForAnotherReason()
    {
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            Acquired, null, animalId: Guid.NewGuid());
        plan.Cancel(Exit.AddDays(-5));

        plan.ReopenAfterExitUndone(Exit).Should().BeFalse();
        plan.Status.Should().Be(FeedingPlanStatus.Cancelled);
    }
}
