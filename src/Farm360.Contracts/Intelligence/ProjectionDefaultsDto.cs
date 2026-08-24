using System;

namespace Farm360.Contracts.Intelligence;

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
    ProjectionDefaultValueDto<int> FatteningPeriodDays);
