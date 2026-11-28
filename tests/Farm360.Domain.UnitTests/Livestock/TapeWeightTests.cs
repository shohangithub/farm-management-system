using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Livestock;

/// <summary>Tape-measure weight: weight (kg) = length (in) × girth (in)² ÷ 660, rounded to 0.1 kg.</summary>
public sealed class TapeWeightTests
{
    [Fact]
    public void EstimateKg_OwnersExample_IsLengthTimesGirthSquaredOver660_RoundedToOneDecimal()
    {
        // 62 × 51 × 51 = 161,262; ÷ 660 = 244.336… → 244.3
        TapeWeight.EstimateKg(62m, 51m).Should().Be(244.3m);
    }

    [Theory]
    [InlineData(60, 60, 327.3)]   // 216,000 ÷ 660 = 327.27…
    [InlineData(66, 50, 250.0)]   // 165,000 ÷ 660 = 250 exactly
    [InlineData(20, 20, 12.1)]    // smallest allowed: 8,000 ÷ 660 = 12.12…
    [InlineData(23, 22, 16.9)]    // 11,132 ÷ 660 = 16.866…
    public void EstimateKg_KnownValues(decimal length, decimal girth, decimal expected)
    {
        TapeWeight.EstimateKg(length, girth).Should().Be(expected);
    }

    [Fact]
    public void EstimateKg_ExactMidpoint_RoundsAwayFromZero()
    {
        // 24.8325 × 20² = 9,933; ÷ 660 = 15.05 exactly → 15.1 (banker's rounding would give 15.0).
        TapeWeight.EstimateKg(24.8325m, 20m).Should().Be(15.1m);
    }

    [Theory]
    [InlineData(19.9, 50)]
    [InlineData(120.1, 50)]
    [InlineData(0, 50)]
    [InlineData(-5, 50)]
    public void EstimateKg_LengthOutOfRange_Throws(decimal length, decimal girth)
    {
        var act = () => TapeWeight.EstimateKg(length, girth);

        act.Should().Throw<InvalidTapeMeasurementException>().WithMessage(TapeWeight.LengthOutOfRangeMessage);
    }

    [Theory]
    [InlineData(60, 19.9)]
    [InlineData(60, 120.1)]
    public void EstimateKg_GirthOutOfRange_Throws(decimal length, decimal girth)
    {
        var act = () => TapeWeight.EstimateKg(length, girth);

        act.Should().Throw<InvalidTapeMeasurementException>().WithMessage(TapeWeight.GirthOutOfRangeMessage);
    }

    [Fact]
    public void EstimateKg_RangeBoundsAreInclusive()
    {
        TapeWeight.EstimateKg(20m, 20m).Should().Be(12.1m);
        TapeWeight.EstimateKg(90m, 120m).Should().Be(1963.6m); // 1,296,000 ÷ 660 = 1963.63…
    }

    [Fact]
    public void EstimateKg_AboveTheWeightCap_Throws()
    {
        // 120 × 120² ÷ 660 = 2618.2 kg — over the 2000 kg cap for any weight.
        var act = () => TapeWeight.EstimateKg(120m, 120m);

        act.Should().Throw<InvalidTapeMeasurementException>().WithMessage(TapeWeight.EstimateTooHighMessage);
    }

    [Fact]
    public void RecordTapeWeight_ComputesKgAndStoresTheMeasurements()
    {
        var animal = CreateAnimal();

        var record = animal.RecordTapeWeight(62m, 51m, new DateOnly(2025, 6, 1), Guid.NewGuid(), "tape");

        record.Method.Should().Be(WeightMethod.Tape);
        record.LengthInch.Should().Be(62m);
        record.GirthInch.Should().Be(51m);
        record.Weight.WeightKg.Should().Be(244.3m);
        animal.LatestWeightKg.Should().Be(244.3m);
    }

    [Fact]
    public void RecordWeight_IsAScaleWeight_WithNoMeasurements()
    {
        var animal = CreateAnimal();

        var record = animal.RecordWeight(Weight.Create(250m), new DateOnly(2025, 6, 1), Guid.NewGuid(), null);

        record.Method.Should().Be(WeightMethod.Scale);
        record.LengthInch.Should().BeNull();
        record.GirthInch.Should().BeNull();
    }

    [Fact]
    public void RecordTapeWeight_OutOfRange_ThrowsAndRecordsNothing()
    {
        var animal = CreateAnimal();

        var act = () => animal.RecordTapeWeight(10m, 51m, new DateOnly(2025, 6, 1), Guid.NewGuid(), null);

        act.Should().Throw<InvalidTapeMeasurementException>();
        animal.WeightRecords.Should().BeEmpty();
    }

    private static Animal CreateAnimal() => Animal.Create(
        tenantId: Guid.NewGuid(),
        farmId: Guid.NewGuid(),
        tag: AnimalTag.Create("T-001", TagType.EarTag),
        species: AnimalSpecies.CattleBeef,
        breedId: Guid.NewGuid(),
        sex: AnimalSex.Male,
        dateOfBirth: new DateOnly(2023, 1, 15),
        acquisitionType: AcquisitionType.Purchased,
        acquisitionDate: new DateOnly(2024, 1, 1),
        acquisitionPriceBdt: 50_000m,
        notes: null);
}
