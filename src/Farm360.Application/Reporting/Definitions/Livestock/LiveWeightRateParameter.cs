using System.Collections.Generic;
using System.Globalization;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>
/// The "live-weight rate" option shared by the animal and herd summary reports.
/// </summary>
/// <remarks>
/// Defaults to the farm's own valuation rate (<see cref="IAnimalValuationService"/>), so the
/// reports agree with the projections and the animal screen out of the box. The fixed rates are
/// what-if overrides for a particular market, not a benchmark.
/// </remarks>
internal static class LiveWeightRateParameter
{
    public const string Name = "pricePerKg";
    public const string FarmRateValue = "farm";

    private static readonly int[] OverrideRates = [350, 380, 400, 420, 450, 500];

    public static ReportParameter Create(IAnimalValuationService valuation)
    {
        var rate = valuation.LiveWeightRatePerKgBdt.ToString("N0", CultureInfo.InvariantCulture);
        var options = new List<ReportSelectOption>
        {
            new(FarmRateValue, new LocalizedText(
                $"Farm valuation rate (৳ {rate} / live kg — {valuation.RateBasisDescription})",
                $"খামারের মূল্যায়ন দর (৳ {rate} / জীবন্ত কেজি)")),
        };

        foreach (var r in OverrideRates)
        {
            var text = r.ToString(CultureInfo.InvariantCulture);
            options.Add(new ReportSelectOption(text, new LocalizedText(
                $"Override: ৳ {text} / live kg",
                $"বিকল্প দর: ৳ {text} / জীবন্ত কেজি")));
        }

        return ReportParameter.Select(
            Name,
            new LocalizedText("Live-weight valuation rate", "জীবন্ত ওজনের মূল্যায়ন দর"),
            options,
            @default: FarmRateValue,
            required: false);
    }

    /// <summary>The user's override rate, or null to use the farm's valuation policy.</summary>
    public static decimal? ReadOverride(ReportContext context)
    {
        var value = context.Number(Name, 0m);
        return value > 0 ? value : null;
    }

    /// <summary>The rate per live kg actually applied to on-farm animals.</summary>
    public static decimal EffectiveRate(ReportContext context, IAnimalValuationService valuation) =>
        ReadOverride(context) ?? valuation.LiveWeightRatePerKgBdt;
}
