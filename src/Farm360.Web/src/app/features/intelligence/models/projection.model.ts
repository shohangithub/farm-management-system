export interface ProjectionDefaultValue<T> {
  value: T;
  source: string; // SystemDefault, FarmSetting, BreedStandard, AnimalRecord, ManualOverride
  notes: string;
}

export interface ProjectionDefaults {
  startingLiveWeightKg: ProjectionDefaultValue<number>;
  purchasePriceBdt: ProjectionDefaultValue<number>;
  currentMeatPriceBdtPerKg: ProjectionDefaultValue<number>;
  initialMeatYieldRatio: ProjectionDefaultValue<number>;
  dailyLiveWeightGainKg: ProjectionDefaultValue<number>;
  meatYieldOnDailyGainRatio: ProjectionDefaultValue<number>;
  dailyFeedQuantityKgAtStart: ProjectionDefaultValue<number>;
  feedPriceBdtPerKg: ProjectionDefaultValue<number>;
  dailyGrassCostBdt: ProjectionDefaultValue<number>;
  dailyOtherCostBdt: ProjectionDefaultValue<number>;
  monthlyLaborCostBdt: ProjectionDefaultValue<number>;
  fatteningPeriodDays: ProjectionDefaultValue<number>;
}

export interface FatteningProjectionInputs {
  startingLiveWeightKg: number;
  purchasePriceBdt: number;
  currentMeatPriceBdtPerKg: number;
  initialMeatYieldRatio: number;
  dailyLiveWeightGainKg: number;
  meatYieldOnDailyGainRatio: number;
  dailyFeedQuantityKgAtStart: number;
  feedPriceBdtPerKg: number;
  dailyGrassCostBdt: number;
  dailyOtherCostBdt: number;
  monthlyLaborCostBdt: number;
  fatteningPeriodDays: number;
}

export interface ProfitProjectionDayResult {
  day: number;
  liveWeightKg: number;
  meatWeightKg: number;
  feedQtyKg: number;
  feedCostBdt: number;
  grassCostBdt: number;
  otherCostBdt: number;
  laborCostBdt: number;
  dailyTotalCostBdt: number;
  meatGainKg: number;
  meatValueBdt: number;
  cumulativeCostBdt: number;
  totalInvestmentBdt: number;
  profitLossBdt: number;
  profitPercent: number;
}

export interface ProfitProjectionSummary {
  startingWeightKg: number;
  finalWeightKg: number;
  totalGainKg: number;
  purchaseCostBdt: number;
  totalFeedCostBdt: number;
  totalGrassCostBdt: number;
  totalOtherCostBdt: number;
  totalLaborCostBdt: number;
  totalFarmingCostBdt: number;
  totalInvestmentBdt: number;
  finalMeatWeightKg: number;
  expectedSaleValueBdt: number;
  profitLossBdt: number;
  profitPercent: number;
  breakEvenPricePerLiveKgBdt: number;
  breakEvenPricePerMeatKgBdt: number;
  breakEvenDay: number | null;
  optimalSaleDay: number | null;
  optimalProfitBdt: number | null;
  totalFeedQtyKg: number;
  costPerKgGainBdt: number;
  roiPercent: number;
  dailyProfitRateBdt: number;
  meatPriceUsedBdtPerKg: number;
}

export interface ProfitProjectionResponse {
  summary: ProfitProjectionSummary;
  days: ProfitProjectionDayResult[];
}
