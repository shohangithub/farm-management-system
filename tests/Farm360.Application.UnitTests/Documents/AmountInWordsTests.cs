using Farm360.Application.Documents;
using Farm360.Application.Reporting.Model;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Documents;

public class AmountInWordsTests
{
    [Theory]
    [InlineData(0, "Taka Zero Only")]
    [InlineData(7, "Taka Seven Only")]
    [InlineData(12_500, "Taka Twelve Thousand Five Hundred Only")]
    [InlineData(80_000, "Taka Eighty Thousand Only")]
    [InlineData(125_050, "Taka One Lakh Twenty-Five Thousand Fifty Only")]
    [InlineData(23_456_789, "Taka Two Crore Thirty-Four Lakh Fifty-Six Thousand Seven Hundred Eighty-Nine Only")]
    public void English_uses_the_lakh_and_crore_scale(int amount, string expected)
    {
        AmountInWords.English(amount).Should().Be(expected);
    }

    [Fact]
    public void English_prints_paisa()
    {
        AmountInWords.English(1_250.50m).Should().Be("Taka One Thousand Two Hundred Fifty and Fifty Paisa Only");
    }

    [Theory]
    [InlineData(12_500, "বারো হাজার পাঁচ শত টাকা মাত্র")]
    [InlineData(80_000, "আশি হাজার টাকা মাত্র")]
    [InlineData(125_099, "এক লক্ষ পঁচিশ হাজার নিরানব্বই টাকা মাত্র")]
    [InlineData(20_000_000, "দুই কোটি টাকা মাত্র")]
    public void Bangla_uses_the_lakh_and_crore_scale(int amount, string expected)
    {
        AmountInWords.Bangla(amount).Should().Be(expected);
    }

    [Fact]
    public void Bangla_prints_paisa()
    {
        AmountInWords.Bangla(80_000.25m).Should().Be("আশি হাজার টাকা পঁচিশ পয়সা মাত্র");
    }

    [Fact]
    public void For_picks_the_document_language()
    {
        AmountInWords.For(500m, ReportLanguage.En).Should().Be("Taka Five Hundred Only");
        AmountInWords.For(500m, ReportLanguage.Bn).Should().Be("পাঁচ শত টাকা মাত্র");
    }
}
