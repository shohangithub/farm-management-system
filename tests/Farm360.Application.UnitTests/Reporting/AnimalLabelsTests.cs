using Farm360.Application.Reporting.Definitions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class AnimalLabelsTests
{
    private static Animal NewAnimal() =>
        Animal.Create(Guid.NewGuid(), Guid.NewGuid(), AnimalTag.Create("B-04", TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 8, 21), 50_000m, null);

    [Fact]
    public void An_animal_on_the_farm_is_named_by_its_tag() =>
        AnimalLabels.Tag(NewAnimal()).Should().Be("B-04");

    [Fact]
    public void A_sold_animal_carries_its_sale_date()
    {
        var animal = NewAnimal();
        animal.Sell(80_000m, new DateOnly(2026, 11, 12), Guid.NewGuid(), null, null);

        AnimalLabels.Tag(animal).Should().Be("B-04 (Sold 12-Nov-2026)");
    }

    [Fact]
    public void A_dead_animal_carries_its_death_date()
    {
        var animal = NewAnimal();
        animal.RecordDeath(DisposalReason.Disease, new DateOnly(2026, 11, 2), null);

        AnimalLabels.Tag(animal).Should().Be("B-04 (Died 02-Nov-2026)");
    }

    [Fact]
    public void In_a_Bangla_report_the_sold_suffix_is_Bangla_with_Bengali_digits()
    {
        var animal = NewAnimal();
        animal.Sell(80_000m, new DateOnly(2026, 11, 12), Guid.NewGuid(), null, null);

        AnimalLabels.Tag(animal, ReportLanguage.Bn, bengaliNumerals: true).Should().Be("B-04 (বিক্রি ১২ নভেম্বর ২০২৬)");
        AnimalLabels.Tag(animal, ReportLanguage.Bn, bengaliNumerals: false).Should().Be("B-04 (বিক্রি 12 নভেম্বর 2026)");
        AnimalLabels.Tag(animal, ReportLanguage.En, bengaliNumerals: false).Should().Be("B-04 (Sold 12-Nov-2026)");
    }

    [Fact]
    public void In_a_Bangla_report_the_died_and_slaughtered_suffixes_are_Bangla()
    {
        var dead = NewAnimal();
        dead.RecordDeath(DisposalReason.Disease, new DateOnly(2026, 9, 2), null);

        AnimalLabels.Tag(dead, ReportLanguage.Bn, bengaliNumerals: true).Should().Be("B-04 (মৃত্যু ২ সেপ্টেম্বর ২০২৬)");
        AnimalLabels.Tag("B-05", AnimalStatus.Slaughtered, new DateOnly(2026, 8, 2), ReportLanguage.Bn, bengaliNumerals: true)
            .Should().Be("B-05 (জবাই ২ আগস্ট ২০২৬)");
    }

    [Theory]
    [InlineData(1, "জানুয়ারি")]
    [InlineData(8, "আগস্ট")]
    [InlineData(12, "ডিসেম্বর")]
    public void Bangla_dates_use_Bangla_month_names(int month, string name) =>
        AnimalLabels.Day(new DateOnly(2026, month, 5), ReportLanguage.Bn, bengaliNumerals: true).Should().Be($"৫ {name} ২০২৬");
}
