using System;
using System.Linq;
using Farm360.Application.Feeding.Commands.DailyFeedSheets;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

public class FeedSheetSplitterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    [Fact]
    public void Per_head_gives_every_animal_the_entered_amount()
    {
        var herd = new[] { NewAnimal("A"), NewAnimal("B"), NewAnimal("C") };

        var result = FeedSheetSplitter.Split(herd, 6m, FeedSheetQuantityMode.PerHead, FeedSheetSplit.ByWeight, new FarmBusinessRules());

        result.Should().HaveCount(3).And.OnlyContain(r => r.Quantity == 6m);
    }

    [Fact]
    public void Group_total_split_equally_adds_up_to_the_total()
    {
        var herd = new[] { NewAnimal("A"), NewAnimal("B"), NewAnimal("C") };

        var result = FeedSheetSplitter.Split(herd, 10m, FeedSheetQuantityMode.GroupTotal, FeedSheetSplit.Equal, new FarmBusinessRules());

        result.Sum(r => r.Quantity).Should().Be(10m);
        result.Select(r => r.Quantity).Distinct().Count().Should().BeLessThanOrEqualTo(2, "only the rounding remainder differs");
    }

    [Fact]
    public void Group_total_split_by_weight_gives_heavier_animals_more_and_adds_up()
    {
        var heavy = NewAnimal("H");
        heavy.RecordWeight(Weight.Create(400m), new DateOnly(2026, 11, 1), Guid.NewGuid(), null);
        var light = NewAnimal("L");
        light.RecordWeight(Weight.Create(100m), new DateOnly(2026, 11, 1), Guid.NewGuid(), null);

        var rules = new FarmBusinessRules { FeedAllocation = FeedAllocationMethod.LiveWeight };
        var result = FeedSheetSplitter.Split([heavy, light], 50m, FeedSheetQuantityMode.GroupTotal, FeedSheetSplit.ByWeight, rules);

        result.Sum(r => r.Quantity).Should().Be(50m);
        result.Single(r => r.Animal == heavy).Quantity.Should().Be(40m);
        result.Single(r => r.Animal == light).Quantity.Should().Be(10m);
    }

    private static Animal NewAnimal(string tag) =>
        Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 1, 1), 50_000m, null);
}

public class FeedSheetCopyModeTests
{
    [Fact]
    public void Equal_amounts_and_an_equal_split_remainder_copy_as_per_head()
    {
        Farm360.Application.Feeding.Queries.DailyFeedSheets.GetLastFeedSheetQueryHandler
            .IsSameForEveryone([3.333m, 3.333m, 3.334m]).Should().BeTrue();
        Farm360.Application.Feeding.Queries.DailyFeedSheets.GetLastFeedSheetQueryHandler
            .IsSameForEveryone([6m, 6m, 6m]).Should().BeTrue();
    }

    [Fact]
    public void Amounts_that_vary_with_the_animal_copy_as_a_group_total() =>
        Farm360.Application.Feeding.Queries.DailyFeedSheets.GetLastFeedSheetQueryHandler
            .IsSameForEveryone([40m, 10m]).Should().BeFalse();
}
