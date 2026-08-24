namespace Farm360.Domain.Intelligence.Projections;

public sealed record FatteningProjectionInputs(
    decimal StartingLiveWeightKg,
    decimal PurchasePriceBdt,
    decimal CurrentMeatPriceBdtPerKg,
    decimal InitialMeatYieldRatio,
    decimal DailyLiveWeightGainKg,
    decimal MeatYieldOnDailyGainRatio,
    decimal DailyFeedQuantityKgAtStart,
    decimal FeedPriceBdtPerKg,
    decimal DailyGrassCostBdt,
    decimal DailyOtherCostBdt,
    decimal MonthlyLaborCostBdt,
    int FatteningPeriodDays);
