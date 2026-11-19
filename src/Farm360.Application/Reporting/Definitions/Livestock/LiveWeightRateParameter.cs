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
/// reports agree with the projections and the animal screen out of the box. An entered rate is a
/// what-if override for a particular market, not a benchmark.
/// </remarks>
internal static class LiveWeightRateParameter
{
    public const string Name = "pricePerKg";
    public const string FarmRateValue = "farm";

    /// <summary>
    /// A free numeric override rather than a fixed list: market prices move every week and differ
    /// by district, so any list baked into the code is wrong somewhere. Blank means "use the farm's
    /// own valuation rate", which is the default.
    /// </summary>
    public static ReportParameter Create(IAnimalValuationService valuation)
    {
        var rate = valuation.LiveWeightRatePerKgBdt.ToString("N0", CultureInfo.InvariantCulture);
        return new ReportParameter(
            Name,
            new LocalizedText(
                $"Live-weight rate override, ৳/kg (blank = farm rate ৳ {rate})",
                $"জীবন্ত ওজনের দর, ৳/কেজি (খালি = খামারের দর ৳ {rate})"),
            ReportParameterType.Number,
            Required: false,
            DefaultValue: null,
            HelpText: $"Leave blank to use the farm valuation rate (৳ {rate} / live kg — {valuation.RateBasisDescription}). Enter a rate to see a what-if valuation.");
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
