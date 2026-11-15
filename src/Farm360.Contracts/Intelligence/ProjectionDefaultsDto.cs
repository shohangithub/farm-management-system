using System;

namespace Farm360.Contracts.Intelligence;

/// <param name="CostToDateBdt">
/// What the animal has cost so far (acquisition + feed + vet + labour + overhead from the cost
/// ledger). The projection starts from the latest weight, so this — not the bare purchase price —
/// is the money already sunk into the animal at day 0.
/// </param>
/// <param name="LiveWeightValuePerKgBdt">
/// The farm's live-weight valuation rate under its valuation policy (meat price × dressing % for
/// MeatYieldMarket, the live-animal rate for LiveWeightMarket). Used for "if sold today" figures
/// so they agree with the projection engine.
/// </param>
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
    ProjectionDefaultValueDto<decimal>? LiveWeightValuePerKgBdt = null);
