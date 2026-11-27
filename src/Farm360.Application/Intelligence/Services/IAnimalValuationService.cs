using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Livestock;

namespace Farm360.Application.Intelligence.Services;

/// <summary>
/// An animal's cost to date, split into the cost-ledger buckets.
/// <see cref="TotalBdt"/> is always the sum of the parts, so a report that prints the buckets
/// and the total can never disagree with itself.
/// </summary>
public sealed record AnimalCostBreakdown(
    decimal PurchaseBdt,
    decimal FeedBdt,
    decimal VetBdt,
    decimal LabourBdt,
    decimal OverheadBdt)
{
    public decimal TotalBdt => PurchaseBdt + FeedBdt + VetBdt + LabourBdt + OverheadBdt;
}

/// <summary>
/// The one place that answers "what has this animal cost" and "what is it worth", under the
/// farm's configured <see cref="AnimalValuationPolicy"/>. Projections, snapshots and every report
/// use it so a figure printed in one place matches the same figure printed in another.
/// </summary>
public interface IAnimalValuationService
{
    /// <summary>The farm's valuation policy.</summary>
    AnimalValuationPolicy Policy { get; }

    /// <summary>Meat price per carcass kg (farm rule, guarded against misconfiguration).</summary>
    decimal MeatPricePerKgBdt { get; }

    /// <summary>Dressing (carcass) yield as a 0–1 fraction (farm rule, guarded).</summary>
    decimal DressingRatio { get; }

    /// <summary>By-products as a 0–0.5 fraction of meat value (farm rule; meat-yield method only).</summary>
    decimal ByProductsPercent { get; }

    /// <summary>By-products per head in taka (farm rule; meat-yield method only).</summary>
    decimal ByProductsPerHeadBdt { get; }

    /// <summary>
    /// Added once to an animal's value on top of weight × <see cref="LiveWeightRatePerKgBdt"/>: the
    /// by-products per head under the meat-yield method, else 0.
    /// </summary>
    decimal PerHeadAdditionBdt { get; }

    /// <summary>
    /// The farm rule's value of one kg of live weight (step 3 of <see cref="Pricing.IAnimalPriceResolver"/>,
    /// which puts own sales and the breed price first): the live-animal rate under
    /// <see cref="AnimalValuationPolicy.LiveWeightMarket"/>, otherwise meat price × dressing % × (1 + by-products %).
    /// Under <see cref="AnimalValuationPolicy.CostBasis"/> this is still the market rate used for
    /// forward projections; current value is the cost itself.
    /// </summary>
    decimal LiveWeightRatePerKgBdt { get; }

    /// <summary>Short human-readable basis for <see cref="LiveWeightRatePerKgBdt"/>.</summary>
    string RateBasisDescription { get; }

    /// <summary>
    /// Purchase price: the ledger's acquisition bucket when it has one, else the price on the
    /// animal record.
    /// </summary>
    decimal PurchaseCostBdt(Animal animal, AnimalCostLedger? ledger);

    /// <summary>
    /// Cost to date from the cost ledger (acquisition + feed + vet + labour + overhead).
    /// Without a ledger: purchase price plus <paramref name="fallbackFeedCostBdt"/>.
    /// </summary>
    AnimalCostBreakdown CostToDate(Animal animal, AnimalCostLedger? ledger, decimal fallbackFeedCostBdt = 0m);

    /// <summary>
    /// Market value of a live animal of this weight. <paramref name="costBasisBdt"/> is returned
    /// under the cost-basis policy; <paramref name="ratePerKgOverride"/>, when positive, replaces
    /// the farm rate (a report's "what if the market is ৳ X / kg" option).
    /// </summary>
    decimal ValueAtWeight(decimal liveWeightKg, decimal costBasisBdt, decimal? ratePerKgOverride = null);

    /// <summary>
    /// What the animal is worth today, by status: on-farm (Active / Quarantined) animals are
    /// valued at their latest weight; Sold or Slaughtered animals are worth what they realised
    /// (ledger sale revenue, else the sale price on the record); Dead animals are worth nothing.
    /// </summary>
    decimal CurrentValue(Animal animal, AnimalCostLedger? ledger, decimal costToDateBdt, decimal? ratePerKgOverride = null);

    /// <summary>
    /// As <see cref="CurrentValue(Animal, AnimalCostLedger?, decimal, decimal?)"/>, but an on-farm animal is
    /// valued with <paramref name="price"/> from <see cref="Pricing.IAnimalPriceResolver"/> (own sales, breed
    /// price, then the farm rule).
    /// </summary>
    decimal CurrentValue(Animal animal, AnimalCostLedger? ledger, decimal costToDateBdt, Pricing.AnimalPrice price, decimal? ratePerKgOverride = null);

    /// <summary>True for animals still on the farm, whose value is an estimate rather than realised.</summary>
    bool IsOnHand(Animal animal);
}
