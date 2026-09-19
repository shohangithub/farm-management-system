using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>
/// End-to-end cover for GAP-1 (docs/32): a group-fed animal must end up with a real, non-zero
/// feed cost instead of the silent zero the plan→animal join used to return.
/// </summary>
public class FeedAllocationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid RuleSetId = Guid.NewGuid();
    private static readonly Guid FormulaId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 9, 19);

    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly TestRules _rules = new();
    private readonly FeedAllocationService _sut;

    public FeedAllocationServiceTests()
    {
        _sut = new FeedAllocationService(_animals, _rules);
    }

    private sealed class TestRules : IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Rules { get; } = new();
        public FarmBusinessRules Current => Rules;
    }

    private static Animal AnimalWeighing(string tag, decimal weightKg)
    {
        var animal = Animal.Create(
            tenantId: TenantId,
            farmId: FarmId,
            tag: AnimalTag.Create(tag, TagType.EarTag),
            species: AnimalSpecies.CattleBeef,
            breedId: Guid.NewGuid(),
            sex: AnimalSex.Male,
            dateOfBirth: new DateOnly(2024, 1, 1),
            acquisitionType: AcquisitionType.Purchased,
            acquisitionDate: new DateOnly(2025, 1, 1),
            acquisitionPriceBdt: 50_000m,
            notes: null);

        animal.RecordWeight(Weight.Create(weightKg), Today.AddDays(-1), Guid.NewGuid(), null);
        return animal;
    }

    private static AnimalFeedingPlan BatchPlan() => new(
        Guid.NewGuid(), TenantId, FarmId, RuleSetId, FeedingPlanType.FixedQuantity,
        Today.AddDays(-30), null, animalId: null, batchId: BatchId);

    private static AnimalFeedingPlan IndividualPlan(Guid animalId) => new(
        Guid.NewGuid(), TenantId, FarmId, RuleSetId, FeedingPlanType.FixedQuantity,
        Today.AddDays(-30), null, animalId: animalId);

    private static DailyFeedingEntry Entry(AnimalFeedingPlan plan, decimal expectedKg, decimal unitCost)
    {
        var entry = new DailyFeedingEntry(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            feedingPlanId: plan.Id,
            farmId: FarmId,
            entryDate: Today,
            formulaId: FormulaId,
            expectedKg: expectedKg,
            ruleLineId: Guid.NewGuid(),
            shedId: plan.ShedId,
            penId: plan.PenId,
            batchId: plan.BatchId);

        entry.SetConsumptionCost(unitCost);
        return entry;
    }

    private void HerdInBatch(params Animal[] animals) =>
        _animals.GetActiveByScopeAcrossTenantsAsync(TenantId, BatchId, null, null, Today, Arg.Any<CancellationToken>())
            .Returns(animals.ToList());

    // ── The bug GAP-1 describes ─────────────────────────────────────────────

    [Fact]
    public async Task GroupFedAnimals_EachReceiveANonZeroFeedCost()
    {
        // Before GAP-1 was closed, every one of these animals reported zero feed cost, because
        // the plan names a batch and the animal→plan join found nothing.
        var herd = new[] { AnimalWeighing("B-001", 180m), AnimalWeighing("B-002", 240m), AnimalWeighing("B-003", 305m) };
        HerdInBatch(herd);

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3m, unitCost: 44.30m), plan);

        allocations.Should().HaveCount(3);
        allocations.Should().OnlyContain(a => a.AllocatedCostBdt > 0m);
        allocations.Should().OnlyContain(a => a.Origin == FeedAllocationOrigin.GroupPlanShare);
        allocations.Select(a => a.AnimalId).Should().BeEquivalentTo(herd.Select(h => h.Id));
    }

    [Fact]
    public async Task GroupTotal_IsTheRationTimesHeadCount_NotTheRationAlone()
    {
        // The rule line is a per-animal ration ("200-250 kg → 3 kg/day"), so three head eat 9 kg,
        // not 3 kg. Treating the entry as the group's total would understate feed threefold.
        HerdInBatch(AnimalWeighing("B-001", 200m), AnimalWeighing("B-002", 200m), AnimalWeighing("B-003", 200m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3m, unitCost: 44.30m), plan);

        allocations.Sum(a => a.AllocatedKg).Should().Be(9m);
        allocations.Sum(a => a.AllocatedCostBdt).Should().Be(398.70m, "9 kg × 44.30");
    }

    [Fact]
    public async Task WholeGroupBasis_DividesTheEntryInsteadOfMultiplyingIt()
    {
        _rules.Rules.GroupPlanQuantityBasis = GroupPlanQuantityBasis.WholeGroup;
        HerdInBatch(AnimalWeighing("B-001", 200m), AnimalWeighing("B-002", 200m), AnimalWeighing("B-003", 200m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 9m, unitCost: 44.30m), plan);

        allocations.Sum(a => a.AllocatedKg).Should().Be(9m);
        allocations.Should().OnlyContain(a => a.AllocatedKg == 3m);
    }

    [Fact]
    public async Task HeavierAnimalsCarryMoreOfTheFeedBill()
    {
        HerdInBatch(AnimalWeighing("B-001", 150m), AnimalWeighing("B-002", 450m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 4m, unitCost: 50m), plan);

        var light = allocations.Single(a => a.WeightAtAllocationKg == 150m);
        var heavy = allocations.Single(a => a.WeightAtAllocationKg == 450m);

        heavy.AllocatedCostBdt.Should().BeGreaterThan(light.AllocatedCostBdt);
        (heavy.AllocatedKg / light.AllocatedKg).Should().BeApproximately(2.28m, 0.05m, "3^0.75 = 2.28");
        allocations.Sum(a => a.AllocatedKg).Should().Be(8m, "2 head × 4 kg ration");
    }

    [Fact]
    public async Task AllocationsAreLosslessAgainstTheGroupTotal()
    {
        // 7 head and a recurring division: the sum must still land exactly on the group total.
        var herd = Enumerable.Range(1, 7)
            .Select(i => AnimalWeighing($"B-{i:000}", 120m + (i * 37m)))
            .ToArray();
        HerdInBatch(herd);

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3.3m, unitCost: 44.37m), plan);

        var expectedKg = 3.3m * 7;
        allocations.Sum(a => a.AllocatedKg).Should().Be(expectedKg);
        allocations.Sum(a => a.AllocatedCostBdt).Should().Be(decimal.Round(expectedKg * 44.37m, 2));
    }

    // ── Individual plans still behave ───────────────────────────────────────

    [Fact]
    public async Task IndividualPlan_GivesTheWholeRationToItsAnimal()
    {
        var animal = AnimalWeighing("B-100", 260m);
        _animals.GetByIdAcrossTenantsAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var plan = IndividualPlan(animal.Id);
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3.5m, unitCost: 44m), plan);

        allocations.Should().ContainSingle();
        allocations[0].AnimalId.Should().Be(animal.Id);
        allocations[0].AllocatedKg.Should().Be(3.5m);
        allocations[0].AllocatedCostBdt.Should().Be(154m);
        allocations[0].Origin.Should().Be(FeedAllocationOrigin.IndividualPlan);
        allocations[0].HeadCountAtAllocation.Should().Be(1);
    }

    // ── Edge cases that must not lose or invent money ───────────────────────

    [Fact]
    public async Task EmptyScope_AllocatesNothingRatherThanInventingAnAnimal()
    {
        HerdInBatch();

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3m, unitCost: 44.30m), plan);

        allocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ActualConsumption_OverridesThePlannedQuantity()
    {
        HerdInBatch(AnimalWeighing("B-001", 200m), AnimalWeighing("B-002", 200m));

        var plan = BatchPlan();
        var entry = Entry(plan, expectedKg: 3m, unitCost: 40m);
        entry.Confirm(2.5m, null, "Refusal after rain");

        var allocations = await _sut.BuildAllocationsAsync(entry, plan);

        allocations.Sum(a => a.AllocatedKg).Should().Be(5m, "2 head × 2.5 kg actually eaten");
    }

    [Fact]
    public async Task EachAllocationRecordsHowItWasComputed()
    {
        // Provenance is what makes a printed cost defensible a year later.
        HerdInBatch(AnimalWeighing("B-001", 180m), AnimalWeighing("B-002", 240m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 3m, unitCost: 44.30m), plan);

        allocations.Should().OnlyContain(a => a.Method == FeedAllocationMethod.MetabolicWeight);
        allocations.Should().OnlyContain(a => a.HeadCountAtAllocation == 2);
        allocations.Should().OnlyContain(a => a.WeightAtAllocationKg > 0m);
        allocations.Should().OnlyContain(a => a.ShareFactor > 0m);
        allocations.Should().OnlyContain(a => a.EntryDate == Today);
        allocations.Should().OnlyContain(a => a.BatchId == BatchId);
        allocations.Should().OnlyContain(a => !a.IsBackfilled);
    }

    [Fact]
    public async Task ChangingTheRuleToEqualSplit_ChangesFutureAllocationsOnly()
    {
        // Proves the assumption is configuration, not a hard-coded constant.
        _rules.Rules.FeedAllocation = FeedAllocationMethod.EqualPerHead;
        HerdInBatch(AnimalWeighing("B-001", 150m), AnimalWeighing("B-002", 450m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, expectedKg: 4m, unitCost: 50m), plan);

        allocations.Should().OnlyContain(a => a.AllocatedKg == 4m);
        allocations.Should().OnlyContain(a => a.Method == FeedAllocationMethod.EqualPerHead);
    }

    [Fact]
    public async Task BackfilledAllocationsAreFlagged()
    {
        HerdInBatch(AnimalWeighing("B-001", 200m));

        var plan = BatchPlan();
        var allocations = await _sut.BuildAllocationsAsync(Entry(plan, 3m, 44m), plan, isBackfill: true);

        allocations.Should().OnlyContain(a => a.IsBackfilled);
    }
}
