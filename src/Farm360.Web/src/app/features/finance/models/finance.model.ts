export interface FinancialTransaction {
  id: string;
  farmId: string;
  type: 'Income' | 'Expense' | string;
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

export interface PagedFinancialTransactionsResult {
  items: FinancialTransaction[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netCashFlowBdt: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface FinancialTransactionParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  type?: string;
  category?: string;
  startDate?: string;
  endDate?: string;
  animalId?: string;
  batchId?: string;
  sortBy?: string;
  sortDesc?: boolean;
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

export interface UpdateFinancialTransactionRequest {
  category: string;
  amountBdt: number;
  transactionDate: string;
  description: string;
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
  tagId?: string;
  targetPrice10PercentMarginPerKg?: number;
  targetPrice20PercentMarginPerKg?: number;
  targetPrice30PercentMarginPerKg?: number;
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

export interface MonthlyCashFlowPoint {
  monthLabel: string;
  incomeBdt: number;
  expenseBdt: number;
  netProfitBdt: number;
}

export interface CategoryExpenseBreakdown {
  category: string;
  amountBdt: number;
  percentage: number;
}

export interface FinancialDashboard {
  farmId: string;
  revenueMtdBdt: number;
  expensesMtdBdt: number;
  netProfitMtdBdt: number;
  revenueMomPercent: number;
  expensesMomPercent: number;
  netProfitMomPercent: number;
  revenueYtdBdt?: number;
  expensesYtdBdt?: number;
  netProfitYtdBdt?: number;
  profitMarginPercent?: number;
  cashFlowTrend?: MonthlyCashFlowPoint[];
  expenseBreakdown?: CategoryExpenseBreakdown[];
  recentTransactions?: FinancialTransaction[];
}

export const TRANSACTION_CATEGORIES = {
  Expense: [
    'AnimalPurchase',
    'FeedCost',
    'VeterinaryCost',
    'MedicineCost',
    'LaborCost',
    'Utilities',
    'Transport',
    'InventoryPurchase',
    'MiscellaneousExpense'
  ],
  Income: [
    'AnimalSale',
    'MilkSale',
    'ByproductSale',
    'OtherIncome'
  ]
};
