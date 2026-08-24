using System;
using System.Collections.Generic;
using System.Linq;

namespace Farm360.Domain.Intelligence.Projections;

public static class FatteningProjectionCalculator
{
    public static FatteningProjectionResult Calculate(FatteningProjectionInputs inputs)
    {
        FatteningProjectionValidator.Validate(inputs);

        var days = new List<FatteningProjectionDay>(inputs.FatteningPeriodDays);

        decimal cumulativeCost = 0;
        decimal dailyFlatCost = inputs.DailyGrassCostBdt + inputs.DailyOtherCostBdt + (inputs.MonthlyLaborCostBdt / 30m);

        int? breakEvenDay = null;
        int optimalSaleDay = 1;
        decimal optimalProfitBdt = decimal.MinValue;

        decimal totalFeedCost = 0;
        decimal totalGrassCost = 0;
        decimal totalOtherCost = 0;
        decimal totalLaborCost = 0;
        decimal totalFeedQtyKg = 0;

        for (int day = 1; day <= inputs.FatteningPeriodDays; day++)
        {
            decimal liveWeightKg = inputs.StartingLiveWeightKg + (inputs.DailyLiveWeightGainKg * day);
            decimal meatWeightKg = liveWeightKg * inputs.InitialMeatYieldRatio;
            
            // Feed quantity scales with body weight
            decimal feedQtyKg = inputs.DailyFeedQuantityKgAtStart * (liveWeightKg / inputs.StartingLiveWeightKg);
            decimal feedCostBdt = feedQtyKg * inputs.FeedPriceBdtPerKg;
            
            decimal dailyTotalCostBdt = feedCostBdt + dailyFlatCost;
            cumulativeCost += dailyTotalCostBdt;

            decimal meatGainKg = inputs.DailyLiveWeightGainKg * inputs.MeatYieldOnDailyGainRatio;
            decimal meatValueBdt = meatWeightKg * inputs.CurrentMeatPriceBdtPerKg;
            
            decimal totalInvestmentBdt = inputs.PurchasePriceBdt + cumulativeCost;
            decimal profitLossBdt = meatValueBdt - totalInvestmentBdt;
            
            decimal profitPercent = totalInvestmentBdt == 0 ? 0 : profitLossBdt / totalInvestmentBdt;

            if (profitLossBdt >= 0 && !breakEvenDay.HasValue)
            {
                breakEvenDay = day;
            }

            if (profitLossBdt > optimalProfitBdt)
            {
                optimalProfitBdt = profitLossBdt;
                optimalSaleDay = day;
            }

            totalFeedCost += feedCostBdt;
            totalGrassCost += inputs.DailyGrassCostBdt;
            totalOtherCost += inputs.DailyOtherCostBdt;
            totalLaborCost += (inputs.MonthlyLaborCostBdt / 30m);
            totalFeedQtyKg += feedQtyKg;

            days.Add(new FatteningProjectionDay(
                day,
                liveWeightKg,
                meatWeightKg,
                feedQtyKg,
                feedCostBdt,
                inputs.DailyGrassCostBdt,
                inputs.DailyOtherCostBdt,
                inputs.MonthlyLaborCostBdt / 30m,
                dailyTotalCostBdt,
                meatGainKg,
                meatValueBdt,
                cumulativeCost,
                totalInvestmentBdt,
                profitLossBdt,
                profitPercent
            ));
        }

        var lastDay = days.Last();
        decimal totalGainKg = inputs.DailyLiveWeightGainKg * inputs.FatteningPeriodDays;
        decimal totalFarmingCostBdt = cumulativeCost;
        decimal breakEvenPricePerLiveKgBdt = lastDay.TotalInvestmentBdt / lastDay.LiveWeightKg;
        decimal breakEvenPricePerMeatKgBdt = lastDay.TotalInvestmentBdt / lastDay.MeatWeightKg;
        decimal costPerKgGainBdt = totalGainKg > 0 ? totalFarmingCostBdt / totalGainKg : 0;
        decimal roiPercent = lastDay.TotalInvestmentBdt > 0 ? lastDay.ProfitLossBdt / lastDay.TotalInvestmentBdt : 0;
        decimal dailyProfitRateBdt = lastDay.ProfitLossBdt / inputs.FatteningPeriodDays;

        var summary = new FatteningProjectionSummary(
            inputs.StartingLiveWeightKg,
            lastDay.LiveWeightKg,
            totalGainKg,
            inputs.PurchasePriceBdt,
            totalFeedCost,
            totalGrassCost,
            totalOtherCost,
            totalLaborCost,
            totalFarmingCostBdt,
            lastDay.TotalInvestmentBdt,
            lastDay.MeatWeightKg,
            lastDay.MeatValueBdt,
            lastDay.ProfitLossBdt,
            lastDay.ProfitPercent,
            breakEvenPricePerLiveKgBdt,
            breakEvenPricePerMeatKgBdt,
            breakEvenDay,
            optimalSaleDay,
            optimalProfitBdt,
            totalFeedQtyKg,
            costPerKgGainBdt,
            roiPercent,
            dailyProfitRateBdt,
            inputs.CurrentMeatPriceBdtPerKg
        );

        return new FatteningProjectionResult(inputs, days, summary);
    }

    public static decimal SolveForRequiredMeatPrice(FatteningProjectionInputs inputs)
    {
        // MeatPrice * MeatYield * FinalWeight = TotalInvestment
        // MeatPrice = TotalInvestment / (MeatYield * FinalWeight)
        // From Phase 1.4: BreakEven = TotalInvestment / FinalMeatWeight
        var result = Calculate(inputs);
        return result.Summary.BreakEvenPricePerMeatKgBdt;
    }
}
