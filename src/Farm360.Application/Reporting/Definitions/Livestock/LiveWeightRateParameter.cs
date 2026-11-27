using System.Globalization;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>
/// The "live-weight rate" option shared by the animal and herd summary reports.
/// </summary>
/// <remarks>
/// Blank values each animal with <see cref="Farm360.Application.Intelligence.Pricing.IAnimalPriceResolver"/>,
/// so the reports agree with the animal page and the Sell-or-Keep advice out of the box. An entered rate is a
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
                $"Live-weight rate override, ৳/kg (blank = each animal's own price)",
                $"জীবন্ত ওজনের দর, ৳/কেজি (খালি = প্রতিটি পশুর নিজের দাম)"),
            ReportParameterType.Number,
            Required: false,
            DefaultValue: null,
            HelpText: $"Leave blank to value each animal at its own price, as on the animal page: your own sales of its breed in the last 12 months, else the breed price, else the farm rule (৳ {rate} / live kg — {valuation.RateBasisDescription}). Enter a rate to see a what-if valuation.");
    }

    /// <summary>"As of … · valued at …" for the report subject line.</summary>
    public static string SubjectBasis(ReportContext context, IAnimalValuationService valuation)
    {
        var bn = context.Language == ReportLanguage.Bn;
        if (ReadOverride(context) is { } entered)
        {
            var r = entered.ToString("N0", CultureInfo.InvariantCulture);
            return bn ? $"খামারে থাকা পশুর মূল্য ৳ {r}/কেজি জীবন্ত ওজন দরে" : $"animals on the farm valued at ৳ {r} per live kg";
        }

        if (valuation.Policy == Farm360.Domain.BusinessRules.AnimalValuationPolicy.CostBasis)
            return bn ? "খামারে থাকা পশুর মূল্য: নিজের বিক্রি বা জাতের দাম, না থাকলে খরচে" : "animals on the farm valued at own sales or breed price, else at cost";

        var rate = valuation.LiveWeightRatePerKgBdt.ToString("N0", CultureInfo.InvariantCulture);
        return bn
            ? $"খামারে থাকা পশুর মূল্য: নিজের বিক্রি বা জাতের দাম, না থাকলে ৳ {rate}/কেজি"
            : $"animals on the farm valued at own sales or breed price, else ৳ {rate} per live kg";
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
