export interface ProjectionDefaultValue<T> {
  value: T;
  source: string; // SystemDefault, FarmSetting, BreedStandard, AnimalRecord, InventoryAverage, ManualOverride
  sourceLabel: string;
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
  /** Everything already spent on the animal (acquisition + feed + vet + labour + overhead). */
  costToDateBdt?: ProjectionDefaultValue<number> | null;
  /** The animal's resolved price per kg live weight (kept for older screens; see marketPrice). */
  liveWeightValuePerKgBdt?: ProjectionDefaultValue<number> | null;
  /** What this animal sells for, from the app's one price resolver (same as the Sell-or-Keep card). */
  marketPrice?: AnimalMarketPrice | null;
}

/** Mirrors Farm360.Contracts.Intelligence.AnimalMarketPriceDto. */
export interface AnimalMarketPrice {
  pricePerKgBdt: number;
  /** Added once to the animal's value (by-products under the meat-yield rule), else 0. */
  perHeadBdt: number;
  source: 'OwnSales' | 'BreedExpected' | 'FarmLiveRate' | 'MeatAndDressing' | 'CostBasis';
  salesCount: number;
  /** The farm values animals at cost: no market value to show. */
  valuedAtCost: boolean;
  explanationEn: string;
  explanationBn: string;
}
