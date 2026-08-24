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
  totalAnimals: number;
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
  dataPoints: { label: string; adgValue: number }[];
}

export interface FeedCostTrend {
  groupName: string;
  dataPoints: { label: string; costPerAnimal: number }[];
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
