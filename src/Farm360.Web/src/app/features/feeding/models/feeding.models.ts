export enum FeedCategory {
  Forage = 1,
  Concentrate = 2,
  Mineral = 3,
  Additive = 4,
  Silage = 5,
  Byproduct = 6
}

export const FeedCategoryNames: Record<number, string> = {
  [FeedCategory.Forage]: 'Forage',
  [FeedCategory.Concentrate]: 'Concentrate',
  [FeedCategory.Mineral]: 'Mineral Supplement',
  [FeedCategory.Additive]: 'Additive / Premix',
  [FeedCategory.Silage]: 'Silage',
  [FeedCategory.Byproduct]: 'Agro Byproduct'
};

export enum TargetAnimalType {
  Cattle = 1,
  Goat = 2,
  Sheep = 3,
  Buffalo = 4
}

export const TargetAnimalTypeNames: Record<number, string> = {
  [TargetAnimalType.Cattle]: 'Cattle',
  [TargetAnimalType.Goat]: 'Goat',
  [TargetAnimalType.Sheep]: 'Sheep',
  [TargetAnimalType.Buffalo]: 'Buffalo'
};

export enum ScheduleFrequency {
  OnceDaily = 1,
  TwiceDaily = 2,
  ThriceDaily = 3,
  AdLibitum = 4
}

export const ScheduleFrequencyNames: Record<number, string> = {
  [ScheduleFrequency.OnceDaily]: 'Once Daily (1x)',
  [ScheduleFrequency.TwiceDaily]: 'Twice Daily (2x)',
  [ScheduleFrequency.ThriceDaily]: 'Thrice Daily (3x)',
  [ScheduleFrequency.AdLibitum]: 'Ad Libitum (Free Choice)'
};

export enum FormulaStatus {
  Draft = 1,
  Active = 2,
  Archived = 3
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
  unit?: string;
  description?: string;
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
