export enum InsightType {
  Prediction = 0,
  Optimization = 1,
  Anomaly = 2,
  Risk = 3,
  Performance = 4
}

export enum InsightSeverity {
  Info = 0,
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4
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
  actionableInsights: ActionableInsight[];
}
