/** Mirrors Farm360.Application.Intelligence.SellOrKeep.SellOrKeepAdvice (enums arrive as strings). */
export type SellOrKeepVerdict = 'SellNow' | 'Keep' | 'LosingMoney' | 'WeighFirst';
export type SellOrKeepConfidence = 'Low' | 'Medium' | 'High';
export type SellOrKeepPriceSource = 'OwnSales' | 'BreedExpected' | 'FarmLiveRate' | 'MeatAndDressing' | 'CostBasis';

/** Mirrors Farm360.Application.Intelligence.Pricing.AnimalPrice — the one price resolver, shared with the animal page. */
export interface AnimalPrice {
  pricePerKgBdt: number;
  source: SellOrKeepPriceSource;
  salesCount: number;
  /** Added once to the animal's value (by-products under the meat-yield rule), else 0. */
  perHeadBdt: number;
  valuedAtCost?: boolean;
  explanationEn: string;
  explanationBn: string;
}
export type SellOrKeepGrowthVsTarget = 'Unknown' | 'Below' | 'At' | 'Above';
export type SellOrKeepLossCause = 'None' | 'Sickness' | 'SlowGrowth' | 'FeedCost' | 'PurchasePrice';
export type SellOrKeepBreedTargetBasis = 'None' | 'StandardRange' | 'GoodCommercialFarm' | 'AverageFarm';
/** Where a cost per day came from. */
export type SellOrKeepCostBasis = 'None' | 'RecentRecords' | 'LifetimeAverage' | 'Override';

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
  breedTargetBasis: SellOrKeepBreedTargetBasis;
  breedTargetMinKg?: number | null;
  breedTargetMaxKg?: number | null;
  growthVsTarget: SellOrKeepGrowthVsTarget;
  /** (last − first weighing) ÷ days: the animal page header's ADG. */
  lifetimeAdgKgPerDay?: number | null;
  firstWeighingDate?: string | null;
  /** Least-squares slope over the last {recentGrowthWindowDays} days. */
  recentAdgKgPerDay?: number | null;
  recentGrowthWindowDays: number;
  /** The gain the projection starts from (a what-if, else adgKgPerDay). */
  projectionAdgKgPerDay?: number | null;
  adgOverridden: boolean;
  growthSlowdownPercent: number;
  growthDeclineDays: number;
  feedPerDayBdt: number;
  labourOverheadPerDayBdt: number;
  healthPerDayBdt: number;
  dailyCostBdt: number;
  feedCostBasis: SellOrKeepCostBasis;
  labourOverheadCostBasis: SellOrKeepCostBasis;
  healthCostBasis: SellOrKeepCostBasis;
  feedWindowDays: number;
  healthWindowDays: number;
  feedCostPerKgGainBdt?: number | null;
  /** Daily cost ÷ the projection's gain per day. */
  costPerKgGainBdt?: number | null;
  price: AnimalPrice;
  priceOverridden: boolean;
  horizonDays: number;
  bestDay?: number | null;
  bestDate?: string | null;
  bestWeightKg?: number | null;
  profitAtBestDayBdt?: number | null;
  extraProfitBdt?: number | null;
  breakEvenDay?: number | null;
  breakEvenDate?: string | null;
  roiAtBestDayPercent?: number | null;
  weightAtHorizonKg?: number | null;
  costAtHorizonBdt?: number | null;
  profitAtHorizonBdt?: number | null;
  breakEvenPricePerKgAtHorizonBdt?: number | null;
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

/** What-if overrides for the Fattening plan; anything left out uses the animal's own figures. */
export interface FatteningPlanOverrides {
  pricePerKg?: number | null;
  perHead?: number | null;
  adg?: number | null;
  feedPerDay?: number | null;
  labourPerDay?: number | null;
  healthPerDay?: number | null;
  horizonDays?: number | null;
  growthSlowdownPercent?: number | null;
}

/** Mirrors Farm360.Application.Intelligence.SellOrKeep.FatteningPlan. */
export interface FatteningPlan {
  /** The animal's own figures — identical to the Sell-or-keep card (no projection). */
  defaults: SellOrKeepAdvice;
  /** With the overrides applied, including the day-by-day projection. */
  plan: SellOrKeepAdvice;
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

type Phrase = { text: string; params?: Record<string, string | number> };

/** True when the first weighing was within a week of arrival, so "since first weighing" = "since arrival". */
export function firstWeighingIsArrival(a: Pick<SellOrKeepAdvice, 'firstWeighingDate' | 'today' | 'daysOnFarm'>): boolean {
  if (!a.firstWeighingDate) return false;
  const arrival = Date.parse(a.today) - a.daysOnFarm * 86_400_000;
  return Date.parse(a.firstWeighingDate) - arrival <= 7 * 86_400_000;
}

/** Which gain per day a figure is ("last 60 days, 3 weighings", "since arrival", "typed in"). */
export function adgBasisText(a: SellOrKeepAdvice, forProjection = false): Phrase {
  if (forProjection && a.adgOverridden) return { text: 'typed in (what-if)' };
  switch (a.growthSource) {
    case 'Recent': return { text: 'last {days} days, {count} weighings', params: { days: a.recentGrowthWindowDays, count: a.growthWeighings } };
    case 'Lifetime': return { text: firstWeighingIsArrival(a) ? 'since arrival (too few recent weighings)' : 'since the first weighing (too few recent weighings)' };
    default: return { text: 'no growth recorded yet' };
  }
}

/** Where the breed's growth target comes from (all breed ADG figures are kg per day). */
export function breedTargetSourceText(a: SellOrKeepAdvice): Phrase {
  const breed = a.breedName || '—';
  switch (a.breedTargetBasis) {
    case 'StandardRange':
      return { text: 'Breed ADG target for {breed}: the middle of its standard range, {min}–{max} kg/day', params: { breed, min: (a.breedTargetMinKg ?? 0).toFixed(2), max: (a.breedTargetMaxKg ?? 0).toFixed(2) } };
    case 'GoodCommercialFarm': return { text: 'Breed ADG target for {breed}: its gain on a good commercial farm', params: { breed } };
    case 'AverageFarm': return { text: 'Breed ADG target for {breed}: its gain on an average farm', params: { breed } };
    default: return { text: 'No breed target set' };
  }
}

/** Where a cost per day came from. */
export function costBasisText(basis: SellOrKeepCostBasis, kind: 'feed' | 'labour' | 'health', windowDays: number): Phrase {
  switch (basis) {
    case 'Override': return { text: 'typed in (what-if)' };
    case 'LifetimeAverage': return { text: kind === 'feed' ? 'lifetime feed ÷ days on the farm (nothing in the last {days} days)' : 'lifetime labour & overhead ÷ days on the farm', params: { days: windowDays } };
    case 'RecentRecords':
      return kind === 'feed' ? { text: 'from your feed records, last {days} days', params: { days: windowDays } }
        : kind === 'labour' ? { text: 'from labour & overhead shares, last {days} days', params: { days: windowDays } }
        : { text: 'from treatments & vaccinations, last {days} days', params: { days: windowDays } };
    default: return { text: kind === 'feed' ? 'no feed recorded yet' : kind === 'labour' ? 'no labour or overhead booked yet' : 'no treatments in the last {days} days', params: { days: windowDays } };
  }
}
