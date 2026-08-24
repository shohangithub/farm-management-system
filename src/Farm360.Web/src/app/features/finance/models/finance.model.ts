export interface FinancialTransaction {
  id: string;
  farmId: string;
  type: string;
  category: string;
  amountBdt: number;
  transactionDate: string;
  description: string;
  referenceId?: string;
  notes?: string;
  animalId?: string;
  batchId?: string;
  shedId?: string;
  createdAtUtc: string;
}

export interface FinancialTransactionSummary {
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netBalanceBdt: number;
}

export interface RecordIncomeRequest {
  category: string;
  amountBdt: number;
  transactionDate: string;
  description: string;
  referenceId?: string;
  notes?: string;
  animalId?: string;
  batchId?: string;
  shedId?: string;
}

export interface RecordExpenseRequest {
  category: string;
  amountBdt: number;
  transactionDate: string;
  description: string;
  referenceId?: string;
  notes?: string;
  animalId?: string;
  batchId?: string;
  shedId?: string;
}

export interface LoanRecord {
  id: string;
  farmId: string;
  lenderName: string;
  principalAmountBdt: number;
  interestRatePercent: number;
  disbursementDate: string;
  schedule: string;
  totalRepaidBdt: number;
  outstandingBalanceBdt: number;
  repaymentProgressPercent: number;
  notes?: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateLoanRecordRequest {
  lenderName: string;
  principalAmountBdt: number;
  interestRatePercent: number;
  disbursementDate: string;
  schedule: string;
  notes?: string;
}

export interface RecordLoanRepaymentRequest {
  amountBdt: number;
  repaymentDate: string;
  referenceId?: string;
  notes?: string;
}

export interface AnimalCostLedger {
  animalId: string;
  farmId: string;
  acquisitionCostBdt: number;
  totalFeedCostBdt: number;
  totalVetCostBdt: number;
  totalLaborCostBdt: number;
  totalOverheadBdt: number;
  totalCostBdt: number;
  saleRevenueBdt?: number;
  profitLossBdt?: number;
}

export interface BreakEvenCalculator {
  animalId: string;
  farmId: string;
  currentWeightKg: number;
  totalAccumulatedCostBdt: number;
  breakEvenPricePerKgBdt: number;
}

export interface BatchPnLReport {
  batchId: string;
  farmId: string;
  totalIncomeBdt: number;
  totalCostBdt: number;
  grossProfitBdt: number;
  returnOnInvestmentPercent: number;
  totalAnimals: number;
}

export interface MonthlyPnLReport {
  farmId: string;
  year: number;
  month: number;
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netProfitBdt: number;
  incomeByCategory: { [key: string]: number };
  expenseByCategory: { [key: string]: number };
}

export interface FarmPnLSnapshot {
  farmId: string;
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netProfitBdt: number;
}

export interface ConsolidatedPnLReport {
  year: number;
  month: number;
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netProfitBdt: number;
  farmBreakdown: { [key: string]: FarmPnLSnapshot };
}

export interface FinancialDashboard {
  farmId: string;
  revenueMtdBdt: number;
  expensesMtdBdt: number;
  netProfitMtdBdt: number;
  revenueMomPercent: number;
  expensesMomPercent: number;
  netProfitMomPercent: number;
}
