export enum FeedCategory {
  Forage = 'Forage',
  Concentrate = 'Concentrate',
  Mineral = 'Mineral',
  Additive = 'Additive',
  Silage = 'Silage',
  Byproduct = 'Byproduct'
}

export const FeedCategoryNames: Record<FeedCategory, string> = {
  [FeedCategory.Forage]: 'Forage',
  [FeedCategory.Concentrate]: 'Concentrate',
  [FeedCategory.Mineral]: 'Mineral Supplement',
  [FeedCategory.Additive]: 'Additive / Premix',
  [FeedCategory.Silage]: 'Silage',
  [FeedCategory.Byproduct]: 'Agro Byproduct'
};

export enum TargetAnimalType {
  Cattle = 'Cattle',
  Goat = 'Goat',
  Sheep = 'Sheep',
  Buffalo = 'Buffalo'
}

export const TargetAnimalTypeNames: Record<TargetAnimalType, string> = {
  [TargetAnimalType.Cattle]: 'Cattle',
  [TargetAnimalType.Goat]: 'Goat',
  [TargetAnimalType.Sheep]: 'Sheep',
  [TargetAnimalType.Buffalo]: 'Buffalo'
};

export enum ScheduleFrequency {
  OnceDaily = 'OnceDaily',
  TwiceDaily = 'TwiceDaily',
  ThriceDaily = 'ThriceDaily',
  AdLibitum = 'AdLibitum'
}

export const ScheduleFrequencyNames: Record<ScheduleFrequency, string> = {
  [ScheduleFrequency.OnceDaily]: 'Once Daily (1x)',
  [ScheduleFrequency.TwiceDaily]: 'Twice Daily (2x)',
  [ScheduleFrequency.ThriceDaily]: 'Thrice Daily (3x)',
  [ScheduleFrequency.AdLibitum]: 'Ad Libitum (Free Choice)'
};

export enum FormulaStatus {
  Draft = 'Draft',
  Active = 'Active',
  Archived = 'Archived'
}

export interface FeedIngredient {
  id: string;
  tenantId: string;
  name: string;
  category: FeedCategory;
  categoryName: string;
  dryMatterPct: number;
  crudeProteinPct: number;
  metabolizableEnergyMjPerKg: number;
  crudeFiberPct: number;
  calciumPct: number;
  phosphorusPct: number;
  unit: string;
  unitCostBdt: number;
  isPreloaded: boolean;
  isActive: boolean;
  description?: string;
  inventoryItemId?: string;
}

export interface FormulaIngredient {
  id: string;
  ingredientId: string;
  ingredientName: string;
  percentage: number;
  ingredientCostPerKg: number;
  dryMatterPct: number;
  crudeProteinPct: number;
  metabolizableEnergyMjPerKg: number;
}

export interface FeedFormula {
  id: string;
  tenantId: string;
  title: string;
  targetSpecies: TargetAnimalType;
  targetSpeciesName: string;
  targetStage?: string;
  status: FormulaStatus;
  statusName: string;
  description?: string;
  totalCostPerKgBdt: number;
  dryMatterPct: number;
  crudeProteinPct: number;
  metabolizableEnergyMjPerKg: number;
  ingredients: FormulaIngredient[];
}

export interface FeedingSchedule {
  id: string;
  tenantId: string;
  farmId: string;
  shedId?: string;
  shedNumber?: string;
  penId?: string;
  penNumber?: string;
  batchId?: string;
  batchName?: string;
  formulaId: string;
  formulaTitle: string;
  title: string;
  targetQuantityKgPerHead: number;
  frequency: ScheduleFrequency;
  frequencyName: string;
  startDate: string;
  endDate?: string;
  isActive: boolean;
  notes?: string;
}

export interface ConsumptionDetail {
  id: string;
  ingredientId: string;
  ingredientName: string;
  offeredKg: number;
  refusalKg: number;
  netConsumedKg: number;
  costBdt: number;
}

export interface FeedConsumptionLog {
  id: string;
  tenantId: string;
  farmId: string;
  shedId?: string;
  shedNumber?: string;
  penId?: string;
  penNumber?: string;
  batchId?: string;
  batchName?: string;
  formulaId: string;
  formulaTitle: string;
  logDate: string;
  headCount: number;
  totalFeedOfferedKg: number;
  totalRefusalKg: number;
  netConsumptionKg: number;
  totalCostBdt: number;
  loggedByUserId?: string;
  notes?: string;
  details: ConsumptionDetail[];
}

export interface FcrAnalytics {
  farmId: string;
  shedId?: string;
  shedNumber?: string;
  totalFeedConsumedKg: number;
  totalWeightGainKg: number;
  fcrValue: number;
  totalFeedCostBdt: number;
  costPerKgGainBdt: number;
  monthlyTrends: MonthlyFcrDataPoint[];
}

export interface MonthlyFcrDataPoint {
  month: string;
  feedConsumedKg: number;
  weightGainKg: number;
  fcrValue: number;
}

export interface CreateFeedIngredientRequest {
  name: string;
  category: FeedCategory;
  dryMatterPct: number;
  crudeProteinPct: number;
  metabolizableEnergyMjPerKg: number;
  crudeFiberPct: number;
  calciumPct: number;
  phosphorusPct: number;
  unitCostBdt: number;
  unit: string;
  description?: string;
  inventoryItemId?: string;
}

export interface CreateFormulaIngredientRequest {
  ingredientId: string;
  percentage: number;
}

export interface CreateFeedFormulaRequest {
  title: string;
  targetSpecies: TargetAnimalType;
  targetStage?: string;
  description?: string;
  ingredients?: CreateFormulaIngredientRequest[];
}

export interface CreateFeedingScheduleRequest {
  farmId: string;
  formulaId: string;
  title: string;
  targetQuantityKgPerHead: number;
  frequency: ScheduleFrequency;
  startDate: string;
  shedId?: string;
  penId?: string;
  batchId?: string;
  endDate?: string;
  notes?: string;
}

export interface LogFeedConsumptionRequest {
  farmId: string;
  formulaId: string;
  logDate: string;
  headCount: number;
  totalFeedOfferedKg: number;
  totalRefusalKg: number;
  shedId?: string;
  penId?: string;
  batchId?: string;
  notes?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// SMART FEEDING MODULE (PHASE 4)
// ─────────────────────────────────────────────────────────────────────────────

export enum FeedingPlanType {
  FixedQuantity = 'FixedQuantity',
  WeightPercentage = 'WeightPercentage',
  AgeBased = 'AgeBased',
  WeightQuantity = 'WeightQuantity'
}

export enum DailyFeedingEntryStatus {
  Pending = 'Pending',
  Confirmed = 'Confirmed',
  Skipped = 'Skipped',
  Adjusted = 'Adjusted'
}

export enum FeedingPlanStatus {
  Draft = 'Draft',
  Active = 'Active',
  Completed = 'Completed',
  Cancelled = 'Cancelled'
}

export enum FeedingPurpose {
  Maintenance = 'Maintenance',
  Growth = 'Growth',
  Gestation = 'Gestation',
  Lactation = 'Lactation',
  Finishing = 'Finishing',
  Starter = 'Starter',
  Transition = 'Transition'
}

export enum ReconciliationStatus {
  Pending = 'Pending',
  Reviewed = 'Reviewed',
  Approved = 'Approved',
  Rejected = 'Rejected'
}

export interface FeedingRuleLine {
  id: string;
  minWeightKg?: number;
  maxWeightKg?: number;
  minAgeDays?: number;
  maxAgeDays?: number;
  feedType: FeedCategory;
  formulaId?: string;
  quantityValue: number;
}

export interface FeedingRuleSet {
  id: string;
  name: string;
  planType: FeedingPlanType;
  targetAnimalType: TargetAnimalType;
  feedingPurpose: FeedingPurpose;
  isActive: boolean;
  baseNotes?: string;
  rules: FeedingRuleLine[];
}

export interface FeedingPlanExclusion {
  id: string;
  exclusionDate: string;
  reason: string;
  resumesOn?: string;
}

export interface AnimalFeedingPlan {
  id: string;
  animalId: string;
  animalTag: string;
  ruleSetId: string;
  ruleSetName: string;
  assignedOn: string;
  canceledOn?: string;
  isActive: boolean;
  expectedDailyFeedKg: number;
  concentrateKgPerDay?: number;
  roughageKgPerDay?: number;
  animalWeightKg?: number;
  animalSpecies?: string;
  formulaName?: string;
  estimatedCostPerKgBdt?: number;
  estimatedDailyCostBdt?: number;
  exclusions: FeedingPlanExclusion[];
}

export interface DailyFeedingEntry {
  id: string;
  animalId: string;
  animalTag: string;
  shedName?: string;
  penName?: string;
  ruleSetId: string;
  targetDate: string;
  expectedKg: number;
  actualKg?: number;
  status: DailyFeedingEntryStatus;
  notes?: string;
  confirmedAtUtc?: string;
  formulaName?: string;
  unitCostBdt?: number;
  totalCostBdt?: number;
}

export interface FeedingEntryIngredientCost {
  ingredientId: string;
  ingredientName: string;
  percentage: number;
  allocatedKg: number;
  unitCostBdt: number;
  totalCostBdt: number;
  inventoryItemName?: string;
  costSource: string;
}

export interface FeedingEntryCostBreakdown {
  entryId: string;
  formulaId: string;
  formulaName: string;
  entryDate: string;
  expectedKg: number;
  actualKg?: number;
  blendedUnitCostBdt?: number;
  totalCostBdt?: number;
  status: string;
  ingredients: FeedingEntryIngredientCost[];
}

export interface FeedingCycleReconciliationLine {
  id: string;
  ruleSetId: string;
  expectedTotalKg: number;
  actualTotalKg: number;
  varianceKg: number;
}

export interface FeedingCycleReconciliation {
  id: string;
  cycleDate: string;
  totalExpectedKg: number;
  totalActualKg: number;
  varianceKg: number;
  status: ReconciliationStatus;
  approvedByUserId?: string;
  approvedAtUtc?: string;
  lines: FeedingCycleReconciliationLine[];
}

export interface CreateFeedingRuleSetRequest {
  name: string;
  planType: FeedingPlanType;
  targetAnimalType: TargetAnimalType;
  feedingPurpose: FeedingPurpose;
  isActive: boolean;
  baseNotes?: string;
  rules: {
    minWeightKg?: number;
    maxWeightKg?: number;
    minAgeDays?: number;
    maxAgeDays?: number;
    feedType: FeedCategory;
    formulaId?: string;
    inventoryItemId?: string;
    quantityValue: number;
  }[];
}

export interface UpdateFeedingRuleSetRequest {
  name: string;
  planType: FeedingPlanType;
  targetAnimalType: TargetAnimalType;
  feedingPurpose: FeedingPurpose;
  isActive: boolean;
  baseNotes?: string;
  rules: {
    minWeightKg?: number;
    maxWeightKg?: number;
    minAgeDays?: number;
    maxAgeDays?: number;
    feedType: FeedCategory;
    formulaId?: string;
    inventoryItemId?: string;
    quantityValue: number;
  }[];
}

export interface AssignAnimalFeedingPlanRequest {
  farmId: string;
  feedingRuleSetIds: string[];
  planType: FeedingPlanType;
  startDate: string;
  animalIds?: string[];
  endDate?: string | null;
  /** A formula or stock item at a fixed kg a day, without a rule set (e.g. an add-on mineral). */
  directRations?: DirectRationRequest[];
  /** Group plan: feeds everyone in the shed / pen / batch, including animals that join later. */
  shedId?: string | null;
  penId?: string | null;
  batchId?: string | null;
  /** End the animals' other feed charts (extras stay). */
  replaceExistingCharts?: boolean;
  expectedDailyFeedKg?: number;
}

export interface DirectRationRequest {
  formulaId?: string | null;
  inventoryItemId?: string | null;
  kgPerDay: number;
}

export interface AnimalDailyFeedingLog {
  id: string;
  entryDate: string;
  formulaId: string;
  formulaName: string;
  expectedKg: number;
  actualKg?: number;
  unitCostAtConsumptionBdt?: number;
  totalCostBdt?: number;
  status: DailyFeedingEntryStatus;
  adjustmentReason?: string;
  ruleSetName?: string;
  shedName?: string;
  penName?: string;
}

export interface AnimalFeedingPlanBrief {
  planId: string;
  ruleSetId: string;
  ruleSetName: string;
  planType: FeedingPlanType;
  startDate: string;
  endDate?: string;
  status: FeedingPlanStatus;
  currentConcentrateKgPerDay?: number;
  currentRoughageKgPerDay?: number;
  triggeredByWeightKg?: number;
}

export interface AnimalFeedingSummary {
  animalId: string;
  totalFeedCostBdt: number;
  totalFeedConsumedKg: number;
  averageCostPerKgBdt: number;
  totalEntriesCount: number;
  confirmedEntriesCount: number;
  pendingEntriesCount: number;
  skippedEntriesCount: number;
  activePlan?: AnimalFeedingPlanBrief;
  plans: AnimalFeedingPlanBrief[];
  entries: AnimalDailyFeedingLog[];
}




// ── Who eats what (board) & Set-ration preview ────────────────────────────

/** One ration feeding an animal: a feed chart, an extra (direct ration) or a group (shed/pen/batch) plan. */
export interface RationItem {
  planId?: string | null;
  ruleSetId: string;
  name: string;
  kind: 'Chart' | 'Extra' | 'Group' | string;
  planType: string;
  feedName: string;
  kgPerDay: number;
  costPerDayBdt: number;
  fit: 'InRange' | 'BelowRange' | 'AboveRange' | 'Unknown' | string;
  isPaused: boolean;
  pausedUntil?: string | null;
}

export interface BoardAnimal {
  animalId: string;
  tag: string;
  species: string;
  shedId?: string | null;
  shedName?: string | null;
  penId?: string | null;
  penName?: string | null;
  batchId?: string | null;
  batchName?: string | null;
  weightKg?: number | null;
  ageDays?: number | null;
  items: RationItem[];
  totalKgPerDay: number;
  totalCostPerDayBdt: number;
}

export interface FeedingBoard {
  farmId: string;
  date: string;
  summary: {
    animals: number;
    fed: number;
    unfed: number;
    withExtras: number;
    paused: number;
    totalKgPerDay: number;
    totalCostPerDayBdt: number;
    activeLegacySchedules: number;
  };
  animals: BoardAnimal[];
}

export interface RationPreviewRequest {
  farmId: string;
  animalIds?: string[] | null;
  ruleSetIds?: string[] | null;
  directRations?: DirectRationRequest[] | null;
  shedId?: string | null;
  penId?: string | null;
  batchId?: string | null;
  replaceExistingCharts?: boolean;
}

export interface PreviewAnimal {
  animalId: string;
  tag: string;
  weightKg?: number | null;
  ageDays?: number | null;
  items: RationItem[];
  totalKgPerDay: number;
  totalCostPerDayBdt: number;
  warnings: string[];
  willEnd: string[];
  alreadyOn: string[];
}

export interface RationPreview {
  animals: PreviewAnimal[];
  groupHeadCount: number;
  totalKgPerDay: number;
  totalCostPerDayBdt: number;
}

export interface ConvertSchedulesResult {
  converted: number;
  skipped: number;
  notes: string[];
}

// ── Feed purchase plan (what the rations need for the next N days) ─────────

export interface FeedRequirementRequest {
  farmId: string;
  days: number;
  fromDate?: string | null;
  animalIds?: string[] | null;
  shedId?: string | null;
  penId?: string | null;
  batchId?: string | null;
  projectGrowth?: boolean;
  includeConsumables?: boolean;
}

export interface ItemNeed {
  inventoryItemId?: string | null;
  name: string;
  category: string;
  baseUnit: string;
  perDay: number;
  total: number;
  inStock: number;
  toBuy: number;
  packName?: string | null;
  packSize?: number | null;
  packsToBuy?: number | null;
  unitCostBdt: number;
  costOfNeedBdt: number;
  costToBuyBdt: number;
  usedIn: string[];
  linkedToStock: boolean;
}

export interface RecipeNeed { formulaId: string; recipe: string; perDayKg: number; totalKg: number; costBdt: number; }
export interface AnimalNeed { animalId: string; tag: string; shed?: string | null; weightKg?: number | null; perDayKg: number; totalKg: number; costBdt: number; }

export interface FeedRequirement {
  farmId: string;
  fromDate: string;
  toDate: string;
  days: number;
  animals: number;
  animalsWithoutRation: number;
  projectedGrowth: boolean;
  items: ItemNeed[];
  recipes: RecipeNeed[];
  perAnimal: AnimalNeed[];
  totalCostOfNeedBdt: number;
  totalCostToBuyBdt: number;
  notes: string[];
}

// ── Nutrition & growth (one animal) ───────────────────────────────────────

export interface NutritionDay {
  date: string;
  weightKg?: number | null;
  weightIsMeasured: boolean;
  feedKg: number;
  dryMatterKg: number;
  proteinG: number;
  energyMj: number;
  fibreG: number;
  calciumG: number;
  phosphorusG: number;
  dryMatterNeedKg?: number | null;
  proteinNeedG?: number | null;
  energyNeedMj?: number | null;
  proteinPercentOfNeed?: number | null;
  energyPercentOfNeed?: number | null;
  recipes: string;
}

export interface NutritionSummary {
  days: number;
  fedDays: number;
  startWeightKg?: number | null;
  endWeightKg?: number | null;
  gainKg?: number | null;
  adgKgPerDay?: number | null;
  totalFeedKg: number;
  totalDryMatterKg: number;
  avgFeedKgPerDay: number;
  avgDryMatterKgPerDay: number;
  avgProteinGPerDay: number;
  avgEnergyMjPerDay: number;
  dietProteinPercent: number;
  dietEnergyMjPerKgDm: number;
  dryMatterPercentOfBodyWeight?: number | null;
  avgProteinPercentOfNeed?: number | null;
  avgEnergyPercentOfNeed?: number | null;
  feedConversionKgDmPerKgGain?: number | null;
  expectedAdgFromEnergy?: number | null;
  verdict: string;
  advice: string[];
}

export interface AnimalNutrition {
  animalId: string;
  tag: string;
  species: string;
  from: string;
  to: string;
  summary: NutritionSummary;
  days: NutritionDay[];
  notes: string[];
}
