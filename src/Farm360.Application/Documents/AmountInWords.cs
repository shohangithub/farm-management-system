using System;
using System.Text;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Documents;

/// <summary>
/// Writes a taka amount in words, the way Bangladeshi receipts do — on the lakh / crore scale:
/// "Taka One Lakh Twenty Thousand Five Hundred Only", "এক লক্ষ বিশ হাজার পাঁচ শত টাকা মাত্র".
/// </summary>
public static class AmountInWords
{
    private const long Crore = 10_000_000;
    private const long Lakh = 100_000;
    private const long Thousand = 1_000;
    private const long Hundred = 100;

    private static readonly string[] EnglishOnes =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] EnglishTens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    /// <summary>Bengali has a distinct word for every number below one hundred.</summary>
    private static readonly string[] BanglaBelowHundred =
    [
        "শূন্য", "এক", "দুই", "তিন", "চার", "পাঁচ", "ছয়", "সাত", "আট", "নয়",
        "দশ", "এগারো", "বারো", "তেরো", "চৌদ্দ", "পনেরো", "ষোলো", "সতেরো", "আঠারো", "উনিশ",
        "বিশ", "একুশ", "বাইশ", "তেইশ", "চব্বিশ", "পঁচিশ", "ছাব্বিশ", "সাতাশ", "আটাশ", "উনত্রিশ",
        "ত্রিশ", "একত্রিশ", "বত্রিশ", "তেত্রিশ", "চৌত্রিশ", "পঁয়ত্রিশ", "ছত্রিশ", "সাঁইত্রিশ", "আটত্রিশ", "উনচল্লিশ",
        "চল্লিশ", "একচল্লিশ", "বিয়াল্লিশ", "তেতাল্লিশ", "চুয়াল্লিশ", "পঁয়তাল্লিশ", "ছেচল্লিশ", "সাতচল্লিশ", "আটচল্লিশ", "উনপঞ্চাশ",
        "পঞ্চাশ", "একান্ন", "বাহান্ন", "তিপ্পান্ন", "চুয়ান্ন", "পঞ্চান্ন", "ছাপ্পান্ন", "সাতান্ন", "আটান্ন", "উনষাট",
        "ষাট", "একষট্টি", "বাষট্টি", "তেষট্টি", "চৌষট্টি", "পঁয়ষট্টি", "ছেষট্টি", "সাতষট্টি", "আটষট্টি", "উনসত্তর",
        "সত্তর", "একাত্তর", "বাহাত্তর", "তিয়াত্তর", "চুয়াত্তর", "পঁচাত্তর", "ছিয়াত্তর", "সাতাত্তর", "আটাত্তর", "উনআশি",
        "আশি", "একাশি", "বিরাশি", "তিরাশি", "চুরাশি", "পঁচাশি", "ছিয়াশি", "সাতাশি", "আটাশি", "উননব্বই",
        "নব্বই", "একানব্বই", "বিরানব্বই", "তিরানব্বই", "চুরানব্বই", "পঁচানব্বই", "ছিয়ানব্বই", "সাতানব্বই", "আটানব্বই", "নিরানব্বই",
    ];

    /// <summary>The amount in words in the document's language.</summary>
    public static string For(decimal amountBdt, ReportLanguage language) =>
        language == ReportLanguage.Bn ? Bangla(amountBdt) : English(amountBdt);

    /// <summary>"Taka Twelve Thousand Five Hundred and Fifty Paisa Only".</summary>
    public static string English(decimal amountBdt)
    {
        var (taka, paisa) = Split(amountBdt);
        var text = new StringBuilder("Taka ");
        text.Append(taka == 0 ? EnglishOnes[0] : EnglishWhole(taka));

        if (paisa > 0)
        {
            text.Append(" and ").Append(EnglishBelowHundred(paisa)).Append(" Paisa");
        }

        return text.Append(" Only").ToString();
    }

    /// <summary>"বারো হাজার পাঁচ শত টাকা পঞ্চাশ পয়সা মাত্র".</summary>
    public static string Bangla(decimal amountBdt)
    {
        var (taka, paisa) = Split(amountBdt);
        var text = new StringBuilder(taka == 0 ? BanglaBelowHundred[0] : BanglaWhole(taka));
        text.Append(" টাকা");

        if (paisa > 0)
        {
            text.Append(' ').Append(BanglaBelowHundred[paisa]).Append(" পয়সা");
        }

        return text.Append(" মাত্র").ToString();
    }

    private static (long Taka, int Paisa) Split(decimal amountBdt)
    {
        var amount = Math.Round(Math.Abs(amountBdt), 2, MidpointRounding.AwayFromZero);
        var taka = (long)Math.Truncate(amount);
        var paisa = (int)((amount - taka) * 100m);
        return (taka, paisa);
    }

    private static string EnglishWhole(long value)
    {
        var parts = new StringBuilder();
        AppendScale(parts, value / Crore, "Crore", EnglishWhole);
        value %= Crore;
        AppendScale(parts, value / Lakh, "Lakh", n => EnglishBelowHundred((int)n));
        value %= Lakh;
        AppendScale(parts, value / Thousand, "Thousand", n => EnglishBelowHundred((int)n));
        value %= Thousand;
        AppendScale(parts, value / Hundred, "Hundred", n => EnglishBelowHundred((int)n));
        value %= Hundred;

        if (value > 0)
        {
            if (parts.Length > 0)
            {
                parts.Append(' ');
            }

            parts.Append(EnglishBelowHundred((int)value));
        }

        return parts.ToString();
    }

    private static string EnglishBelowHundred(int value)
    {
        if (value < 20)
        {
            return EnglishOnes[value];
        }

        var tens = EnglishTens[value / 10];
        return value % 10 == 0 ? tens : tens + "-" + EnglishOnes[value % 10];
    }

    private static string BanglaWhole(long value)
    {
        var parts = new StringBuilder();
        AppendScale(parts, value / Crore, "কোটি", BanglaWhole);
        value %= Crore;
        AppendScale(parts, value / Lakh, "লক্ষ", n => BanglaBelowHundred[n]);
        value %= Lakh;
        AppendScale(parts, value / Thousand, "হাজার", n => BanglaBelowHundred[n]);
        value %= Thousand;
        AppendScale(parts, value / Hundred, "শত", n => BanglaBelowHundred[n]);
        value %= Hundred;

        if (value > 0)
        {
            if (parts.Length > 0)
            {
                parts.Append(' ');
            }

            parts.Append(BanglaBelowHundred[value]);
        }

        return parts.ToString();
    }

    private static void AppendScale(StringBuilder parts, long count, string scale, Func<long, string> words)
    {
        if (count <= 0)
        {
            return;
        }

        if (parts.Length > 0)
        {
            parts.Append(' ');
        }

        parts.Append(words(count)).Append(' ').Append(scale);
    }
}
