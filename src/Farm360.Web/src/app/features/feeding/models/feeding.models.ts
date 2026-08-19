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
  AgeBased = 'AgeBased'
}

export enum DailyFeedingEntryStatus {
  Pending = 'Pending',
  Confirmed = 'Confirmed',
  Skipped = 'Skipped',
  Adjusted = 'Adjusted'
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
  ruleSetId: string;
  ruleSetName: string;
  assignedOn: string;
  canceledOn?: string;
  isActive: boolean;
  expectedDailyFeedKg: number;
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
    quantityValue: number;
  }[];
}

export interface AssignAnimalFeedingPlanRequest {
  farmId: string;
  feedingRuleSetId: string;
  planType: FeedingPlanType;
  startDate: string;
  animalId: string;
  expectedDailyFeedKg?: number;
}

