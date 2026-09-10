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
  isAutomated?: boolean;
  sourceModule?: string;
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
  isAutomated?: boolean;
  sourceModule?: string;
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

// ── Investor & Profit Sharing Models ─────────────────────────────────────────

export interface InvestorTransaction {
  id: string;
  investorId: string;
  farmId: string;
  type: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution' | string;
  amountBdt: number;
  transactionDate: string;
  referenceId?: string;
  notes?: string;
  financialTransactionId?: string;
  createdAtUtc: string;
}

export interface Investor {
  id: string;
  farmId: string;
  name: string;
  email?: string;
  phone?: string;
  nationalId?: string;
  investmentDate: string;
  totalInvestedBdt: number;
  totalWithdrawnBdt: number;
  currentCapitalBdt: number;
  totalProfitPaidBdt: number;
  agreedProfitSharePercentage?: number;
  effectiveSharePercentage: number;
  notes?: string;
  isActive: boolean;
  createdAtUtc: string;
  transactions?: InvestorTransaction[];
}

export interface CreateInvestorRequest {
  name: string;
  initialInvestmentBdt: number;
  investmentDate: string;
  agreedProfitSharePercentage?: number | null;
  email?: string | null;
  phone?: string | null;
  nationalId?: string | null;
  notes?: string | null;
  referenceId?: string | null;
}

export interface UpdateInvestorRequest {
  name: string;
  agreedProfitSharePercentage?: number | null;
  email?: string | null;
  phone?: string | null;
  nationalId?: string | null;
  notes?: string | null;
}

export interface RecordInvestorTransactionRequest {
  type: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution';
  amountBdt: number;
  transactionDate: string;
  referenceId?: string | null;
  notes?: string | null;
}

export interface InvestorShare {
  investorId: string;
  investorName: string;
  investedCapitalBdt: number;
  capitalSharePercentage: number;
  effectiveSharePercentage: number;
  allocatedProfitBdt: number;
  distributedProfitBdt: number;
  undistributedProfitBdt: number;
  netEquityValueBdt: number;
  isActive: boolean;
}

export interface InvestorPnLSummary {
  farmId: string;
  fromDate?: string;
  toDate?: string;
  totalFarmRevenueBdt: number;
  totalFarmExpensesBdt: number;
  netFarmProfitBdt: number;
  charityPercentage: number;
  charityAmountBdt: number;
  distributableProfitBdt: number;
  totalActiveCapitalBdt: number;
  totalDistributedProfitBdt: number;
  totalUndistributedProfitBdt: number;
  investorShares: InvestorShare[];
}

export interface TrialBalanceLine {
  accountCode: string;
  accountName: string;
  categoryGroup: 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense' | string;
  debitBdt: number;
  creditBdt: number;
}

export interface TrialBalance {
  farmId: string;
  asOfDate: string;
  lines: TrialBalanceLine[];
  totalDebitBdt: number;
  totalCreditBdt: number;
  isBalanced: boolean;
  differenceBdt: number;
}

export interface BalanceSheetLine {
  lineCode: string;
  lineName: string;
  amountBdt: number;
  notes?: string | null;
}

export interface BalanceSheetSection {
  sectionName: string;
  totalBdt: number;
  lines: BalanceSheetLine[];
}

export interface BalanceSheet {
  farmId: string;
  asOfDate: string;
  assets: BalanceSheetSection;
  liabilities: BalanceSheetSection;
  equity: BalanceSheetSection;
  totalAssetsBdt: number;
  totalLiabilitiesAndEquityBdt: number;
  isBalanced: boolean;
  differenceBdt: number;
  netWorkingCapitalBdt: number;
}

