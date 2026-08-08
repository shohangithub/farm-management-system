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
