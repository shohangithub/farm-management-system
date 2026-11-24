export enum InsightType {
  Prediction = 'Prediction',
  Optimization = 'Optimization',
  Anomaly = 'Anomaly',
  Risk = 'Risk',
  Performance = 'Performance'
}

export enum InsightSeverity {
  Info = 'Info',
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  Critical = 'Critical'
}

export interface ActionableInsight {
  id: string;
  farmId: string;
  animalId?: string;
  batchId?: string;
  type: InsightType;
  severity: InsightSeverity;
  title: string;
  message: string;
  actionData?: string;
  isRead: boolean;
  createdAtUtc: string;
}

export interface ExecutiveDashboardData {
  /** Animals currently on the farm (Active + Quarantined); excludes sold/dead/slaughtered history. */
  totalAnimals: number;
  /** Distinct on-farm animals in open (unresolved) disease incidents. */
  sickAnimals: number;
  feedLowStockCount: number;
  currentMonthIncome: number;
  currentMonthExpense: number;
  birthsThisMonth: number;
  deathsThisMonth: number;
  dueVaccinations: number;
  pregnantAnimals: number;
  actionableInsights: ActionableInsight[];
}

export interface HerdComposition {
  bySpecies: Record<string, number>;
  byBreed: Record<string, number>;
  bySex: Record<string, number>;
  byStatus: Record<string, number>;
}

export interface TrendPoint {
  label: string;
  value: number; // adgValue or costPerAnimal
}

export interface AdgTrend {
  batchId: string;
  batchName: string;
  /** adgValue is null for a month with no measurable per-animal weight gain. */
  dataPoints: { label: string; adgValue: number | null }[];
}

export interface FeedCostTrend {
  groupName: string;
  /** costPerAnimal is null for a month with no confirmed feed allocations. */
  dataPoints: { label: string; costPerAnimal: number | null }[];
}

export interface VaccinationCompliance {
  completed: number;
  due: number;
  overdue: number;
}

export interface FarmSummaryCard {
  farmId: string;
  farmName: string;
  animalCount: number;
  sickCount: number;
  monthlyRevenue: number;
}

export interface ActivityFeedItem {
  id: string;
  actionType: string;
  entityName: string;
  description: string;
  userName: string;
  timestamp: string;
}

/** Stable keys of the "Get your farm ready" checklist steps, in setup order (server: GetFarmSetupStatusQuery). */
export type FarmSetupStepKey =
  | 'sheds' | 'breeds' | 'animals' | 'weights' | 'inventory'
  | 'recipes' | 'feedChart' | 'plans' | 'accounts' | 'team';

export interface FarmSetupStep {
  key: FarmSetupStepKey;
  isDone: boolean;
  count: number;
}

export interface FarmSetupStatus {
  farmId: string;
  completedSteps: number;
  totalSteps: number;
  isComplete: boolean;
  steps: FarmSetupStep[];
}
