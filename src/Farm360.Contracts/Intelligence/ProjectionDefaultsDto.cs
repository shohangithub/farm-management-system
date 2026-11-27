using System;

namespace Farm360.Contracts.Intelligence;

/// <param name="CostToDateBdt">
/// What the animal has cost so far (acquisition + feed + vet + labour + overhead from the cost
/// ledger). The projection starts from the latest weight, so this — not the bare purchase price —
/// is the money already sunk into the animal at day 0.
/// </param>
/// <param name="LiveWeightValuePerKgBdt">
/// The animal's resolved price per kg live weight (see <paramref name="MarketPrice"/>), kept for older clients.
/// </param>
/// <param name="MarketPrice">The animal's price from the one price resolver, with its source and explanation.</param>
public sealed record ProjectionDefaultsDto(
    ProjectionDefaultValueDto<decimal> StartingLiveWeightKg,
    ProjectionDefaultValueDto<decimal> PurchasePriceBdt,
    ProjectionDefaultValueDto<decimal> CurrentMeatPriceBdtPerKg,
    ProjectionDefaultValueDto<decimal> InitialMeatYieldRatio,
    ProjectionDefaultValueDto<decimal> DailyLiveWeightGainKg,
    ProjectionDefaultValueDto<decimal> MeatYieldOnDailyGainRatio,
    ProjectionDefaultValueDto<decimal> DailyFeedQuantityKgAtStart,
    ProjectionDefaultValueDto<decimal> FeedPriceBdtPerKg,
    ProjectionDefaultValueDto<decimal> DailyGrassCostBdt,
    ProjectionDefaultValueDto<decimal> DailyOtherCostBdt,
    ProjectionDefaultValueDto<decimal> MonthlyLaborCostBdt,
    ProjectionDefaultValueDto<int> FatteningPeriodDays,
    ProjectionDefaultValueDto<decimal>? CostToDateBdt = null,
    ProjectionDefaultValueDto<decimal>? LiveWeightValuePerKgBdt = null,
    AnimalMarketPriceDto? MarketPrice = null);

/// <summary>
/// What this live animal sells for, from the app's one price resolver (own sales of the breed, the breed
/// price, then the farm rule). The animal page's "Profit if sold today" card and the Sell-or-Keep card
/// show the same figure.
/// </summary>
/// <param name="PricePerKgBdt">Taka per kg live weight.</param>
/// <param name="PerHeadBdt">Added once to the animal's value (by-products under the meat-yield rule), else 0.</param>
/// <param name="Source">OwnSales | BreedExpected | FarmLiveRate | MeatAndDressing | CostBasis.</param>
/// <param name="SalesCount">Own sales the average is built on.</param>
/// <param name="ValuedAtCost">The farm values animals at cost: there is no market value to show.</param>
/// <param name="ExplanationEn">One line, e.g. "breed price ৳420/kg".</param>
/// <param name="ExplanationBn">The same in Bangla.</param>
public sealed record AnimalMarketPriceDto(
    decimal PricePerKgBdt,
    decimal PerHeadBdt,
    string Source,
    int SalesCount,
    bool ValuedAtCost,
    string ExplanationEn,
    string ExplanationBn);
