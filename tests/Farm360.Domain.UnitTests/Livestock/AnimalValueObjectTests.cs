using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Livestock;

/// <summary>
/// Unit tests for Livestock value objects: AnimalTag and Weight.
/// Value objects must enforce their own invariants.
/// </summary>
public sealed class AnimalValueObjectTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // AnimalTag
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AnimalTag_Create_ValidInput_ReturnsTag()
    {
        var tag = AnimalTag.Create("B-001", TagType.EarTag);

        tag.TagId.Should().Be("B-001");
        tag.TagType.Should().Be(TagType.EarTag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnimalTag_Create_EmptyTagId_ThrowsArgumentException(string tagId)
    {
        var act = () => AnimalTag.Create(tagId, TagType.EarTag);

        act.Should().Throw<ArgumentException>().WithMessage("*TagId*");
    }

    [Fact]
    public void AnimalTag_Create_TagIdExceeds50Chars_ThrowsArgumentException()
    {
        var longTagId = new string('X', 51);

        var act = () => AnimalTag.Create(longTagId, TagType.EarTag);

        act.Should().Throw<ArgumentException>().WithMessage("*50*");
    }

    [Fact]
    public void AnimalTag_Equality_SameTagIdAndType_AreEqual()
    {
        var tag1 = AnimalTag.Create("B-001", TagType.EarTag);
        var tag2 = AnimalTag.Create("B-001", TagType.EarTag);

        tag1.Should().Be(tag2);
    }

    [Fact]
    public void AnimalTag_Equality_DifferentTagId_AreNotEqual()
    {
        var tag1 = AnimalTag.Create("B-001", TagType.EarTag);
        var tag2 = AnimalTag.Create("B-002", TagType.EarTag);

        tag1.Should().NotBe(tag2);
    }

    [Fact]
    public void AnimalTag_Equality_DifferentTagType_AreNotEqual()
    {
        var tag1 = AnimalTag.Create("B-001", TagType.EarTag);
        var tag2 = AnimalTag.Create("B-001", TagType.Rfid);

        tag1.Should().NotBe(tag2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Weight
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Weight_Create_ValidWeight_ReturnsWeight()
    {
        var weight = Weight.Create(250.5m);

        weight.WeightKg.Should().Be(250.5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Weight_Create_ZeroOrNegative_ThrowsArgumentException(decimal value)
    {
        var act = () => Weight.Create(value);

        act.Should().Throw<ArgumentException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void Weight_Create_Exceeds2000Kg_ThrowsArgumentException()
    {
        var act = () => Weight.Create(2001m);

        act.Should().Throw<ArgumentException>().WithMessage("*2000 kg*");
    }

    [Fact]
    public void Weight_Equality_SameValue_AreEqual()
    {
        var w1 = Weight.Create(200m);
        var w2 = Weight.Create(200m);

        w1.Should().Be(w2);
    }

    [Fact]
    public void Weight_Equality_DifferentValues_AreNotEqual()
    {
        var w1 = Weight.Create(200m);
        var w2 = Weight.Create(201m);

        w1.Should().NotBe(w2);
    }
}
