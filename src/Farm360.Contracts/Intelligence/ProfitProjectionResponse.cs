using System.Collections.Generic;

namespace Farm360.Contracts.Intelligence;

public sealed record ProfitProjectionResponse(
    FatteningProjectionInputsDto Inputs,
    FatteningProjectionSummaryDto Summary,
    IReadOnlyList<FatteningProjectionDayDto> Days);

public sealed record FatteningProjectionSummaryDto(
    decimal StartingWeightKg,
    decimal FinalWeightKg,
    decimal TotalGainKg,
    decimal PurchaseCostBdt,
    decimal TotalFeedCostBdt,
    decimal TotalGrassCostBdt,
    decimal TotalOtherCostBdt,
    decimal TotalLaborCostBdt,
    decimal TotalFarmingCostBdt,
    decimal TotalInvestmentBdt,
    decimal FinalMeatWeightKg,
    decimal ExpectedSaleValueBdt,
    decimal ProfitLossBdt,
    decimal ProfitPercent,
    decimal BreakEvenPricePerLiveKgBdt,
    decimal BreakEvenPricePerMeatKgBdt,
    int? BreakEvenDay,
    int OptimalSaleDay,
    decimal OptimalProfitBdt,
    decimal TotalFeedQtyKg,
    decimal CostPerKgGainBdt,
    decimal RoiPercent,
    decimal DailyProfitRateBdt,
    decimal MeatPriceUsedBdtPerKg);

public sealed record FatteningProjectionDayDto(
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
