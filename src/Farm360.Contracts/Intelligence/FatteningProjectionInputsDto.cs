namespace Farm360.Contracts.Intelligence;

public sealed record FatteningProjectionInputsDto(
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
