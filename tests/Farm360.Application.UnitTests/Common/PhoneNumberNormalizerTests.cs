using Farm360.Application.Common.Phones;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Common;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("01712345678", "+8801712345678")]
    [InlineData("8801712345678", "+8801712345678")]
    [InlineData("+8801712345678", "+8801712345678")]
    [InlineData(" 017-1234-5678 ", "+8801712345678")]
    [InlineData("+880 1712-345678", "+8801712345678")]
    [InlineData("880 17 1234 5678", "+8801712345678")]
    public void Normalize_BangladeshiMobile_AllSpellingsBecomeTheSameCanonicalNumber(string input, string expected)
    {
        PhoneNumberNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("+44 7700-900123", "+447700900123")]
    [InlineData("+15551234567", "+15551234567")]
    [InlineData("0171234567", "0171234567")]   // 10 digits: not a BD mobile, only compacted
    [InlineData("017123456789", "017123456789")] // 12 digits: not a BD mobile
    public void Normalize_OtherNumbers_AreOnlyCompacted(string input, string expected)
    {
        PhoneNumberNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - ")]
    public void Normalize_Empty_ReturnsNull(string? input)
    {
        PhoneNumberNormalizer.Normalize(input).Should().BeNull();
    }

    [Fact]
    public void LookupCandidates_LocalInput_TriesCanonicalFirstThenTypedThenOtherSpellings()
    {
        PhoneNumberNormalizer.LookupCandidates(" 01712345678 ").Should().Equal(
            "+8801712345678", "01712345678", "8801712345678");
    }

    [Fact]
    public void LookupCandidates_CanonicalInput_AlsoFindsAccountsStoredInLocalForm()
    {
        PhoneNumberNormalizer.LookupCandidates("+8801712345678").Should().Equal(
            "+8801712345678", "01712345678", "8801712345678");
    }

    [Fact]
    public void LookupCandidates_ForeignNumber_CanonicalThenTyped()
    {
        PhoneNumberNormalizer.LookupCandidates("+44 7700 900123").Should().Equal("+447700900123", "+44 7700 900123");
    }

    [Fact]
    public void LookupCandidates_Empty_ReturnsNothing()
    {
        PhoneNumberNormalizer.LookupCandidates("  ").Should().BeEmpty();
    }

    [Theory]
    [InlineData("01712345678", true)]
    [InlineData("+880 1712-345678", true)]
    [InlineData("+447700900123", true)]
    [InlineData("12345", false)]
    [InlineData("017-ABC-5678", false)]
    [InlineData("++8801712345678", false)]
    [InlineData(null, false)]
    public void IsValid_ChecksTheCanonicalForm(string? input, bool expected)
    {
        PhoneNumberNormalizer.IsValid(input).Should().Be(expected);
    }
}
