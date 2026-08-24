namespace Farm360.Domain.Intelligence.Projections;

public sealed record FatteningProjectionDay(
    int Day,
    decimal LiveWeightKg,
    decimal MeatWeightKg,
    decimal FeedQtyKg,
    decimal FeedCostBdt,
    decimal GrassCostBdt,
    decimal OtherCostBdt,
    decimal LaborCostBdt,
    decimal DailyTotalCostBdt,
    decimal MeatGainKg,
    decimal MeatValueBdt,
    decimal CumulativeCostBdt,
    decimal TotalInvestmentBdt,
    decimal ProfitLossBdt,
    decimal ProfitPercent);
