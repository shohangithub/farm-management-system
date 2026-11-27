/** Mirrors Farm360.Application.Intelligence.SellOrKeep.SellOrKeepAdvice (enums arrive as strings). */
export type SellOrKeepVerdict = 'SellNow' | 'Keep' | 'LosingMoney' | 'WeighFirst';
export type SellOrKeepConfidence = 'Low' | 'Medium' | 'High';
export type SellOrKeepPriceSource = 'OwnSales' | 'BreedExpected' | 'FarmLiveRate' | 'MeatAndDressing';
export type SellOrKeepGrowthVsTarget = 'Unknown' | 'Below' | 'At' | 'Above';
export type SellOrKeepLossCause = 'None' | 'Sickness' | 'SlowGrowth' | 'FeedCost' | 'PurchasePrice';

export interface SellOrKeepPoint {
  day: number;
  date: string;
  weightKg: number;
  adgKg: number;
  dailyCostBdt: number;
  costBdt: number;
  valueBdt: number;
  profitBdt: number;
}

export interface SellOrKeepAdvice {
  animalId: string;
  tagId: string;
  breedName: string;
  batchId?: string | null;
  batchName?: string | null;
  today: string;
  daysOnFarm: number;
  investedBdt: number;
  weighings: number;
  latestWeightKg?: number | null;
  latestWeightDate?: string | null;
  weightTodayKg?: number | null;
  valueTodayBdt?: number | null;
  profitTodayBdt?: number | null;
  adgKgPerDay?: number | null;
  growthSource: 'None' | 'Recent' | 'Lifetime';
  growthWeighings: number;
  breedTargetAdgKg?: number | null;
  growthVsTarget: SellOrKeepGrowthVsTarget;
  feedPerDayBdt: number;
  labourOverheadPerDayBdt: number;
  healthPerDayBdt: number;
  dailyCostBdt: number;
  feedCostPerKgGainBdt?: number | null;
  price: { pricePerKgBdt: number; source: SellOrKeepPriceSource; salesCount: number };
  bestDay?: number | null;
  bestDate?: string | null;
  bestWeightKg?: number | null;
  profitAtBestDayBdt?: number | null;
  extraProfitBdt?: number | null;
  breakEvenDay?: number | null;
  breakEvenDate?: string | null;
  valueOfDailyGainBdt?: number | null;
  marginPerDayKeptBdt?: number | null;
  treatmentsLast90Days: number;
  healthCostLast90DaysBdt: number;
  frequentlySick: boolean;
  batchDeathsLast90Days: number;
  verdict: SellOrKeepVerdict;
  lossCause: SellOrKeepLossCause;
  confidence: SellOrKeepConfidence;
  reasonEn: string;
  reasonBn: string;
  projection: SellOrKeepPoint[];
}

/** Short verdict label (English source text, translated with the `t` pipe). */
export function sellOrKeepLabel(a: Pick<SellOrKeepAdvice, 'verdict' | 'bestDay'>): { text: string; params?: Record<string, string | number> } {
  switch (a.verdict) {
    case 'SellNow': return { text: 'Sell now' };
    case 'Keep': return { text: 'Keep ~{days} days', params: { days: a.bestDay ?? 0 } };
    case 'LosingMoney': return { text: 'Losing money' };
    default: return { text: 'Weigh first' };
  }
}
