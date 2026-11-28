import { translate } from '../../../core/i18n/i18n.service';

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
  /** Cash, Inventory, Livestock, Payable or Receivable. Only Cash postings move the cash book. */
  settlement?: 'Cash' | 'Inventory' | 'Livestock' | 'Payable' | 'Receivable' | string;
  affectsCash?: boolean;
  accountCode?: string;
  /** Operating, Asset, Capital or Financing. */
  accountGroup?: string;
  /** Asset, Liability, Equity, Revenue or Expense. */
  accountClass?: string;
  /** The money account a cash posting went through. */
  moneyAccountId?: string | null;
  /** A transfer's other leg, or a partner-paid spend's capital contribution. */
  linkedTransactionId?: string | null;
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
  /** Set when not paid on the day — raises a customer invoice. */
  credit?: CreditTerms | null;
  /** The money account the money went into (null = the farm's default). */
  moneyAccountId?: string | null;
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
  /** Set when not paid on the day — raises a supplier bill. */
  credit?: CreditTerms | null;
  /** The money account it was paid from (null = the farm's default). */
  moneyAccountId?: string | null;
  /** Paid directly by this partner: books their capital contribution of the same amount. */
  paidByPartnerId?: string | null;
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
  /** Flat interest over the life of the loan. */
  totalInterestBdt?: number;
  totalPayableBdt?: number;
  principalRepaidBdt?: number;
  interestPaidBdt?: number;
  /** Principal still owed — the balance-sheet liability. */
  outstandingPrincipalBdt?: number;
}

export interface CreateLoanRecordRequest {
  lenderName: string;
  principalAmountBdt: number;
  interestRatePercent: number;
  disbursementDate: string;
  schedule: string;
  notes?: string;
  /** The account the loan money was received into. */
  moneyAccountId?: string | null;
}

export interface RecordLoanRepaymentRequest {
  amountBdt: number;
  repaymentDate: string;
  referenceId?: string;
  notes?: string;
  moneyAccountId?: string | null;
  paidByPartnerId?: string | null;
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

export interface AllocateOverheadRequest {
  from: string;
  to: string;
  isBackfill?: boolean;
}

export interface OverheadAllocationResult {
  transactionsConsidered: number;
  transactionsAllocated: number;
  transactionsSkippedAlreadyAllocated: number;
  transactionsSkippedNoAnimals: number;
  allocationsWritten: number;
  amountAllocatedBdt: number;
  ledgersUpdated: number;
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

const MONTH_NAMES: Record<string, string> = {
  Jan: 'January', Feb: 'February', Mar: 'March', Apr: 'April', May: 'May', Jun: 'June',
  Jul: 'July', Aug: 'August', Sep: 'September', Oct: 'October', Nov: 'November', Dec: 'December',
};
const BENGALI_DIGITS = '০১২৩৪৫৬৭৮৯';

/**
 * A month label from the API ("Aug 2026") in the interface language: unchanged in English,
 * "আগস্ট ২০২৬" in Bangla (month name from the dictionary, Bengali digits for the year).
 */
export function formatMonthLabel(label: string, bangla: boolean, translate: (text: string) => string): string {
  if (!bangla || !label) return label;
  const match = /^([A-Za-z]{3})[a-z]*\s+(\d{4})$/.exec(label.trim());
  if (!match) return translate(label);
  const month = MONTH_NAMES[match[1].charAt(0).toUpperCase() + match[1].slice(1).toLowerCase()];
  const year = match[2].replace(/\d/g, d => BENGALI_DIGITS[Number(d)]);
  return month ? `${translate(month)} ${year}` : label;
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
  /** First day of the fiscal-year-to-date window the YTD figures cover. */
  fiscalYearStart?: string | null;
  fiscalYearStartMonth?: number;
  /** Cash & bank today, split by money account. */
  moneyAccounts?: MoneyAccountBreakdown[] | null;
}

/**
 * Heads a person may enter by hand. Everything else is posted by the module that owns the event
 * (stock in Inventory, animals in Livestock, loans and investors in Finance) so the books stay in
 * step with stock, the herd and the registers. Mirrors AccountChart.Manual*Categories on the server.
 */
export const MANUAL_TRANSACTION_CATEGORIES = {
  Expense: [
    'FeedCost',
    'VeterinaryCost',
    'MedicineCost',
    'LaborCost',
    'Utilities',
    'Transport',
    'MiscellaneousExpense',
    'InterestExpense'
  ],
  // Owner / partner money is no longer entered here: it is booked under Finance → Partners so each
  // partner's capital and share stay right. Old OwnerCapital / OwnerDrawing rows still display.
  Income: [
    'MilkSale',
    'ByproductSale',
    'OtherIncome'
  ]
};

/** Every head, for filtering the ledger (includes system-posted ones). */
export const TRANSACTION_CATEGORIES = {
  Expense: [
    'FeedCost',
    'VeterinaryCost',
    'MedicineCost',
    'LaborCost',
    'Utilities',
    'Transport',
    'MiscellaneousExpense',
    'ConsumableExpense',
    'InventoryWriteOff',
    'CostOfAnimalsSold',
    'LivestockLoss',
    'InterestExpense',
    'AnimalPurchase',
    'InventoryPurchase',
    'LoanRepayment',
    'SupplierPayment',
    'BadDebt',
    'InvestorWithdrawal',
    'ProfitDistribution',
    'OwnerDrawing'
  ],
  Income: [
    'AnimalSale',
    'MilkSale',
    'ByproductSale',
    'OtherIncome',
    'InventoryPurchaseReturn',
    'LoanDisbursement',
    'CustomerReceipt',
    'InvestorCapital',
    'OwnerCapital'
  ]
};

/** Heads that may be bought or sold on credit. Mirrors AccountChart.Credit*Categories on the server. */
export const CREDIT_CATEGORIES = {
  Expense: ['FeedCost', 'VeterinaryCost', 'MedicineCost', 'LaborCost', 'Utilities', 'Transport', 'MiscellaneousExpense', 'InventoryPurchase', 'AnimalPurchase'],
  Income: ['AnimalSale', 'MilkSale', 'ByproductSale', 'OtherIncome'],
};

export function canBeOnCredit(t: Pick<FinancialTransaction, 'type' | 'category' | 'settlement'>): boolean {
  if ((t.settlement ?? 'Cash') !== 'Cash') return false;
  const list = t.type === 'Income' ? CREDIT_CATEGORIES.Income : CREDIT_CATEGORIES.Expense;
  return list.includes(t.category);
}

/**
 * Display names (English / বাংলা) for every head. `en` is also the translation key: in Bangla mode
 * categoryLabel() shows the Bangla name only (see core/i18n/bn/finance.ts).
 */
export const CATEGORY_LABELS: Readonly<Record<string, { en: string; bn: string; hint?: string }>> = {
  FeedCost: { en: 'Feed & Nutrition', bn: 'খাদ্য খরচ', hint: 'Feed bought and used directly (not via stock)' },
  VeterinaryCost: { en: 'Veterinary & Healthcare', bn: 'পশু চিকিৎসা খরচ' },
  MedicineCost: { en: 'Medicines & Vaccines', bn: 'ঔষধ ও টিকা', hint: 'Medicine bought and used directly (not via stock)' },
  LaborCost: { en: 'Labour & Farm Wages', bn: 'শ্রমিক মজুরি' },
  Utilities: { en: 'Utilities & Energy', bn: 'বিদ্যুৎ ও ইউটিলিটি' },
  Transport: { en: 'Transport & Logistics', bn: 'পরিবহন খরচ' },
  MiscellaneousExpense: { en: 'Miscellaneous Expenses', bn: 'বিবিধ খরচ' },
  ConsumableExpense: { en: 'Consumables Used', bn: 'দৈনিক ভোগ্যপণ্য' },
  InventoryWriteOff: { en: 'Stock Write-off', bn: 'মজুদ ক্ষতি / বাতিল' },
  CostOfAnimalsSold: { en: 'Cost of Animals Sold', bn: 'বিক্রিত পশুর ক্রয়মূল্য' },
  LivestockLoss: { en: 'Livestock Mortality Loss', bn: 'পশু মৃত্যুজনিত ক্ষতি' },
  GrowingCostsCarried: {
    en: 'Feed & care of animals still on the farm (carried to next period)',
    bn: 'খামারে থাকা পশুর খাদ্য ও যত্নের খরচ (পরবর্তী সময়ে স্থানান্তরিত)',
  },
  InterestExpense: { en: 'Loan Interest', bn: 'ঋণের সুদ' },
  BadDebt: { en: 'Bad Debts Written Off', bn: 'অনাদায়ী পাওনা' },
  SupplierPayment: { en: 'Supplier Payment (credit bill)', bn: 'সরবরাহকারীকে বকেয়া পরিশোধ' },
  CustomerReceipt: { en: 'Customer Collection (credit sale)', bn: 'ক্রেতার কাছ থেকে বকেয়া আদায়' },
  AnimalPurchase: { en: 'Livestock Purchase', bn: 'পশু ক্রয়' },
  InventoryPurchase: { en: 'Stock Purchase', bn: 'মালামাল ক্রয় (মজুদ)' },
  InventoryPurchaseReturn: { en: 'Stock Returned to Supplier', bn: 'সরবরাহকারীকে ফেরত' },
  LoanDisbursement: { en: 'Loan Received', bn: 'ঋণ গ্রহণ' },
  LoanRepayment: { en: 'Loan Principal Repaid', bn: 'ঋণের আসল পরিশোধ' },
  InvestorCapital: { en: 'Investor Capital', bn: 'বিনিয়োগ মূলধন' },
  InvestorWithdrawal: { en: 'Investor Capital Withdrawal', bn: 'বিনিয়োগ উত্তোলন' },
  ProfitDistribution: { en: 'Profit Distribution', bn: 'লভ্যাংশ বণ্টন' },
  OwnerCapital: { en: "Owner's Capital", bn: 'মালিকের মূলধন', hint: 'Money the owner puts into the farm' },
  OwnerDrawing: { en: "Owner's Drawings", bn: 'মালিকের উত্তোলন', hint: 'Money the owner takes out for personal use' },
  AnimalSale: { en: 'Livestock Sales', bn: 'পশু বিক্রয়' },
  MilkSale: { en: 'Milk & Dairy Sales', bn: 'দুধ বিক্রয়' },
  ByproductSale: { en: 'By-product & Manure Sales', bn: 'উপজাত বিক্রয়' },
  OtherIncome: { en: 'Other Farm Income', bn: 'অন্যান্য আয়' },
};

/** The head's display name in the interface language; the category value itself never changes. */
export function categoryLabel(category: string): string {
  return translate(CATEGORY_LABELS[category]?.en ?? category.replace(/([a-z])([A-Z])/g, '$1 $2'));
}

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
  /** The money account the first contribution goes into (null = the farm's default). */
  moneyAccountId?: string | null;
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
  /** The account a contribution goes INTO, or a withdrawal / profit payout comes OUT of. */
  moneyAccountId?: string | null;
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
  /** 'Non-current' | 'Current' | 'Capital' | 'Reserves' */
  group?: string | null;
  nameBn?: string | null;
  priorAmountBdt?: number | null;
  isDeduction?: boolean;
}

export interface BalanceSheetSection {
  sectionName: string;
  totalBdt: number;
  lines: BalanceSheetLine[];
  priorTotalBdt?: number | null;
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
  currentYearProfitBdt?: number;
  retainedEarningsBroughtForwardBdt?: number;
  reconciliations?: ReconciliationLine[];
  priorDate?: string | null;
  priorTotalAssetsBdt?: number | null;
  priorTotalLiabilitiesAndEquityBdt?: number | null;
  priorDifferenceBdt?: number | null;
  booksStartDate?: string | null;
  fiscalYearStart?: string;
  fiscalYearStartMonth?: number;
  isConfigured?: boolean;
  message?: string | null;
}

// ── Income statement ──────────────────────────────────────────────────────

export interface IncomeStatementLine {
  accountCode: string;
  accountName: string;
  accountNameBn: string;
  amountBdt: number;
  priorAmountBdt?: number | null;
}

export interface IncomeStatementSection {
  key: 'revenue' | 'cost-of-sales' | 'operating-expenses' | 'finance-costs' | string;
  name: string;
  nameBn: string;
  lines: IncomeStatementLine[];
  totalBdt: number;
  priorTotalBdt?: number | null;
}

export interface IncomeStatementTotals {
  revenueBdt: number;
  costOfSalesBdt: number;
  grossProfitBdt: number;
  operatingExpensesBdt: number;
  operatingProfitBdt: number;
  financeCostsBdt: number;
  netProfitBdt: number;
  grossMarginPercent: number;
  netMarginPercent: number;
}

export interface IncomeStatement {
  farmId: string;
  fromDate: string;
  toDate: string;
  priorFromDate?: string | null;
  priorToDate?: string | null;
  sections: IncomeStatementSection[];
  totals: IncomeStatementTotals;
  priorTotals?: IncomeStatementTotals | null;
  fiscalYearStartMonth: number;
  booksStartDate?: string | null;
}

// ── Accounts setup & fixed assets ─────────────────────────────────────────

export interface AccountingSettings {
  farmId: string;
  fiscalYearStartMonth: number;
  booksStartDate?: string | null;
  isConfigured: boolean;
}

export interface AccountOption {
  code: string;
  name: string;
  nameBn: string;
  accountClass: string;
}

export interface AccountingSetupOptions {
  openingAccounts: AccountOption[];
  adjustmentAccounts: AccountOption[];
  profitAndLossAccounts: AccountOption[];
}

export interface BalanceSheetEntry {
  id: string;
  code: string;
  entryType: 'Opening' | 'Adjustment' | string;
  entryDate: string;
  accountCode: string;
  accountName: string;
  amountBdt: number;
  profitAccountCode?: string | null;
  profitAccountName?: string | null;
  reversalDate?: string | null;
  description: string;
  notes?: string | null;
}

export interface SaveBalanceSheetEntryRequest {
  entryType: 'Opening' | 'Adjustment';
  entryDate: string;
  accountCode: string;
  amountBdt: number;
  description: string;
  profitAccountCode?: string | null;
  reversalDate?: string | null;
  notes?: string | null;
}

export interface GenerateOpeningEntriesResult {
  created: number;
  warnings: string[];
}

export type FixedAssetCategory = 'Land' | 'BuildingAndShed' | 'MachineryAndEquipment' | 'Vehicle' | 'FurnitureAndOffice' | 'Other';
export type FixedAssetAcquisition = 'Cash' | 'Credit' | 'OwnerContributed' | 'Opening';
export type DepreciationMethod = 'ReducingBalance' | 'StraightLine';

export interface FixedAsset {
  id: string;
  code: string;
  name: string;
  category: FixedAssetCategory | string;
  acquisition: FixedAssetAcquisition | string;
  acquisitionDate: string;
  costBdt: number;
  method: DepreciationMethod | string;
  ratePercent: number;
  usefulLifeYears: number;
  residualValueBdt: number;
  disposalDate?: string | null;
  disposalProceedsBdt: number;
  location?: string | null;
  notes?: string | null;
  accumulatedDepreciationBdt: number;
  writtenDownValueBdt: number;
}

export interface SaveFixedAssetRequest {
  name: string;
  category: FixedAssetCategory | string;
  acquisition: FixedAssetAcquisition | string;
  acquisitionDate: string;
  costBdt: number;
  method: DepreciationMethod | string;
  ratePercent: number;
  usefulLifeYears: number;
  residualValueBdt: number;
  location?: string | null;
  notes?: string | null;
  credit?: CreditTerms | null;
  /** Paid in cash: the account it was paid from (null = the farm's default). */
  moneyAccountId?: string | null;
  /** Paid directly by a partner (books their capital contribution). */
  paidByPartnerId?: string | null;
}

export interface FixedAssetScheduleLine {
  assetId: string;
  code: string;
  name: string;
  category: string;
  method: string;
  ratePercent: number;
  acquisitionDate: string;
  disposalDate?: string | null;
  costOpeningBdt: number;
  additionsBdt: number;
  disposalsBdt: number;
  costClosingBdt: number;
  depreciationOpeningBdt: number;
  depreciationChargeBdt: number;
  depreciationOnDisposalBdt: number;
  depreciationClosingBdt: number;
  writtenDownValueBdt: number;
}

export interface FixedAssetSchedule {
  farmId: string;
  fromDate: string;
  toDate: string;
  lines: FixedAssetScheduleLine[];
  totals: FixedAssetScheduleLine;
}

// ── Accounting health (books vs registers) ────────────────────────────────

export interface ReconciliationLine {
  key: string;
  accountCode: string;
  account: string;
  accountBn: string;
  booksBdt: number;
  registerBdt: number;
  differenceBdt: number;
  isReconciled: boolean;
  registerSource: string;
  explanation: string;
}

export interface AccountingIssue {
  key: string;
  severity: 'Error' | 'Warning' | string;
  title: string;
  detail: string;
  count: number;
  amountBdt: number;
  canAutoFix: boolean;
  /** The records the issue names (e.g. each money account below zero). */
  items?: { id?: string | null; name: string; amountBdt: number }[] | null;
}

export interface AccountingHealth {
  farmId: string;
  checkedAtUtc: string;
  isHealthy: boolean;
  cashBalanceBdt: number;
  reconciliations: ReconciliationLine[];
  issues: AccountingIssue[];
}

export interface LedgerRepairResult {
  loanDisbursementsPosted: number;
  loanRepaymentsSplit: number;
  animalsResynced: number;
  disposalsPosted: number;
  inventoryAdjustmentBdt: number;
  messages: string[];
  healthAfter: AccountingHealth;
}

// ── Farm Share Market Models ───────────────────────────────────────────────

export interface FarmShareConfig {
  id: string;
  farmId: string;
  totalShares: number;
  sharePriceBdt: number;
  ownerShareCount: number;
  allocatedShareCount: number;
  availableShareCount: number;
  totalValuationBdt: number;
  availableValuationBdt: number;
  ownerEquityValueBdt: number;
  ownerOwnershipPercentage: number;
  minimumPurchaseShares: number;
  isShareSaleOpen: boolean;
  lastValuationDate: string;
  valuationNotes?: string | null;
}

export interface ShareHolding {
  id: string;
  investorId: string;
  investorName: string;
  investorPhone?: string | null;
  investorEmail?: string | null;
  farmId: string;
  shareCount: number;
  averagePurchasePriceBdt: number;
  totalInvestedBdt: number;
  currentValueBdt: number;
  ownershipPercentage: number;
  unrealizedGainLossBdt: number;
  returnOnInvestmentPercent: number;
  certificateNumber?: string | null;
  notes?: string | null;
  isActive: boolean;
}

export interface ShareTransaction {
  id: string;
  farmId: string;
  investorId: string;
  investorName: string;
  type: 'Purchase' | 'Sale' | 'Transfer' | 'Bonus' | 'ValuationAdjustment' | string;
  shareCount: number;
  pricePerShareBdt: number;
  totalAmountBdt: number;
  transactionDate: string;
  counterpartyInvestorId?: string | null;
  counterpartyInvestorName?: string | null;
  referenceId?: string | null;
  notes?: string | null;
  financialTransactionId?: string | null;
  createdAtUtc: string;
}

export interface ShareDistributionSlice {
  label: string;
  shareCount: number;
  percentage: number;
  valueBdt: number;
  colorHex: string;
  isOwner: boolean;
  isAvailable: boolean;
}

export interface ShareMarketOverview {
  farmId: string;
  isConfigured: boolean;
  config?: FarmShareConfig | null;
  totalShareholdersCount: number;
  totalAllocatedShares: number;
  totalCapitalRaisedBdt: number;
  distribution: ShareDistributionSlice[];
  shareholders: ShareHolding[];
  recentTransactions: ShareTransaction[];
}

export interface ConfigureFarmSharesRequest {
  totalShares: number;
  sharePriceBdt: number;
  ownerShareCount: number;
  minimumPurchaseShares?: number;
  isShareSaleOpen?: boolean;
  valuationNotes?: string | null;
}

export interface PurchaseSharesRequest {
  investorId: string;
  shareCount: number;
  pricePerShareBdt?: number | null;
  transactionDate?: string | null;
  referenceId?: string | null;
  notes?: string | null;
}

export interface SellSharesRequest {
  investorId: string;
  shareCount: number;
  pricePerShareBdt?: number | null;
  transactionDate?: string | null;
  referenceId?: string | null;
  notes?: string | null;
}

export interface TransferSharesRequest {
  fromInvestorId: string;
  toInvestorId: string;
  shareCount: number;
  pricePerShareBdt?: number | null;
  transactionDate?: string | null;
  referenceId?: string | null;
  notes?: string | null;
}

export interface UpdateShareValuationRequest {
  newSharePriceBdt: number;
  valuationNotes?: string | null;
}


// ── Income & Expense (cash book) report ────────────────────────────────────

export type AccountGroup = 'Operating' | 'Asset' | 'Capital' | 'Financing' | 'Trade';

export interface AccountHeadSummary {
  code: string;
  category: string;
  name: string;
  nameBn: string;
  group: AccountGroup;
  count: number;
  totalBdt: number;
  percentOfTotal: number;
}

export interface CashBookLine {
  id: string;
  transactionDate: string;
  voucherNo: string;
  type: 'Income' | 'Expense';
  category: string;
  code: string;
  headName: string;
  headNameBn: string;
  group: AccountGroup;
  description: string;
  notes: string;
  amountBdt: number;
  isAutomated: boolean;
  sourceModule?: string | null;
}

export interface DailyCashSummary {
  date: string;
  openingBalanceBdt: number;
  incomeBdt: number;
  expenseBdt: number;
  netBdt: number;
  closingBalanceBdt: number;
  transactionCount: number;
}

export interface IncomeExpenseReport {
  farmId: string;
  fromDate: string;
  toDate: string;
  isSingleDay: boolean;
  openingBalanceBdt: number;
  totalIncomeBdt: number;
  totalExpenseBdt: number;
  netBalanceBdt: number;
  closingBalanceBdt: number;
  operatingIncomeBdt: number;
  operatingExpenseBdt: number;
  /** Operating cash received less operating cash paid — a cash figure, not profit. */
  netOperatingProfitBdt: number;
  /** Net profit for the same days on the income-statement (accrual) basis. */
  netProfitBdt?: number;
  incomeHeads: AccountHeadSummary[];
  expenseHeads: AccountHeadSummary[];
  incomeTransactions: CashBookLine[];
  expenseTransactions: CashBookLine[];
  dailyBreakdown: DailyCashSummary[];
}

// ── Full accounts statement ────────────────────────────────────────────────

export interface CashPosition {
  openingBalanceBdt: number;
  totalInflowBdt: number;
  totalOutflowBdt: number;
  netCashFlowBdt: number;
  closingBalanceBdt: number;
  transactionCount: number;
  /** The closing balance split by money account. */
  accounts?: MoneyAccountBreakdown[] | null;
}

export interface OperatingStatement {
  revenueLines: AccountHeadSummary[];
  expenseLines: AccountHeadSummary[];
  totalRevenueBdt: number;
  totalExpenseBdt: number;
  netOperatingProfitBdt: number;
  profitMarginPercent: number;
}

export interface CostGroup {
  key: string;
  name: string;
  nameBn: string;
  amountBdt: number;
  percentOfTotal: number;
  count: number;
}

export interface CostAnalysis {
  groups: CostGroup[];
  totalOperatingCostBdt: number;
  labourCostBdt: number;
  feedCostBdt: number;
  healthCostBdt: number;
  costOfAnimalsBdt: number;
  overheadCostBdt: number;
  automatedPostingsBdt: number;
  manualPostingsBdt: number;
  averageDailyCostBdt: number;
}

export interface CapitalFlow {
  investorCapitalInBdt: number;
  investorWithdrawalBdt: number;
  profitDistributedBdt: number;
  loanReceivedBdt: number;
  loanRepaidBdt: number;
  netCapitalFlowBdt: number;
  ownerCapitalInBdt: number;
  ownerDrawingBdt: number;
  loanInterestPaidBdt: number;
}

export interface AssetMovement {
  inventoryOpeningBdt: number;
  stockPurchasedBdt: number;
  stockBroughtInBdt: number;
  stockReturnedBdt: number;
  stockConsumedBdt: number;
  stockWrittenOffBdt: number;
  inventoryClosingBdt: number;
  livestockOpeningBdt: number;
  animalsPurchasedBdt: number;
  animalsBroughtInBdt: number;
  costOfAnimalsSoldBdt: number;
  mortalityLossBdt: number;
  livestockClosingBdt: number;
  /** Feed & care carried in the animals on the farm (farm rule; 0 when off). */
  livestockCarriedOpeningBdt?: number;
  livestockCarriedClosingBdt?: number;
}

export interface InvestorAccount {
  investorId: string;
  name: string;
  phone?: string | null;
  isActive: boolean;
  agreedProfitSharePercentage?: number | null;
  openingCapitalBdt: number;
  contributedInPeriodBdt: number;
  withdrawnInPeriodBdt: number;
  closingCapitalBdt: number;
  profitSharePercentage: number;
  profitEntitledInPeriodBdt: number;
  profitPaidInPeriodBdt: number;
  profitPaidLifetimeBdt: number;
  profitPayableBdt: number;
  shareCount: number;
  ownershipPercentage: number;
  shareMarketValueBdt: number;
}

export interface ProfitShareSummary {
  netOperatingProfitBdt: number;
  charityPercentage: number;
  charityAmountBdt: number;
  distributableProfitBdt: number;
  totalEntitledBdt: number;
  distributedInPeriodBdt: number;
  retainedProfitBdt: number;
}

export interface ShareCapitalSummary {
  isConfigured: boolean;
  totalShares: number;
  sharePriceBdt: number;
  ownerShareCount: number;
  allocatedShareCount: number;
  availableShareCount: number;
  totalValuationBdt: number;
  ownerOwnershipPercentage: number;
  sharesIssuedInPeriod: number;
  shareSalesAmountBdt: number;
  sharesBoughtBackInPeriod: number;
  buybackAmountBdt: number;
  sharesTransferredInPeriod: number;
  transactions: ShareTransaction[];
}

export interface LoanAccount {
  loanId: string;
  lenderName: string;
  disbursementDate: string;
  principalAmountBdt: number;
  interestRatePercent: number;
  totalPayableBdt: number;
  repaidInPeriodBdt: number;
  totalRepaidBdt: number;
  outstandingBdt: number;
  principalRepaidBdt: number;
  interestPaidBdt: number;
  outstandingPrincipalBdt: number;
  disbursedInPeriod: boolean;
  isActive: boolean;
}

export interface LoanSummary {
  disbursedInPeriodBdt: number;
  repaidInPeriodBdt: number;
  interestPaidInPeriodBdt: number;
  totalPrincipalBdt: number;
  totalOutstandingBdt: number;
  /** Principal still owed at the end of the period. */
  totalOutstandingPrincipalBdt: number;
  activeLoanCount: number;
  loans: LoanAccount[];
}

export interface PeriodTrend {
  label: string;
  startDate: string;
  endDate: string;
  operatingIncomeBdt: number;
  operatingExpenseBdt: number;
  netOperatingBdt: number;
  capitalInBdt: number;
  capitalOutBdt: number;
  closingBalanceBdt: number;
}

export interface LedgerEntry {
  id: string;
  transactionDate: string;
  voucherNo: string;
  code: string;
  headName: string;
  group: AccountGroup;
  description: string;
  inflowBdt: number;
  outflowBdt: number;
  runningBalanceBdt: number;
  isAutomated: boolean;
  sourceModule?: string | null;
}

export interface FullAccountsReport {
  farmId: string;
  fromDate: string;
  toDate: string;
  days: number;
  trendGranularity: 'Daily' | 'Monthly';
  cash: CashPosition;
  operating: OperatingStatement;
  costs: CostAnalysis;
  capital: CapitalFlow;
  assets: AssetMovement;
  investors: InvestorAccount[];
  profitShare: ProfitShareSummary;
  shares: ShareCapitalSummary;
  loans: LoanSummary;
  trend: PeriodTrend[];
  ledger: LedgerEntry[];
  credit?: TradeCreditMovement | null;
}

// ── Payables & Receivables (credit book) ─────────────────────────────────────

/** Credit terms for a purchase or sale not (fully) paid on the day. */
export interface CreditTerms {
  partyName: string;
  partyPhone?: string | null;
  /** Defaults to 30 days after the deal. */
  dueDate?: string | null;
  /** Any part paid on the day. */
  paidNowBdt?: number;
  paymentMethod?: string | null;
  notes?: string | null;
  /** The money account the part paid / received now goes through. */
  moneyAccountId?: string | null;
}

export interface OpenTradeBillRequest extends CreditTerms {
  transactionId: string;
}

export interface RecordBillPaymentRequest {
  amountBdt: number;
  paymentDate: string;
  method?: string | null;
  reference?: string | null;
  notes?: string | null;
  /** Account the money goes out of (supplier) or into (customer). */
  moneyAccountId?: string | null;
  /** A partner paid the supplier directly. */
  paidByPartnerId?: string | null;
}

export interface WriteOffBillRequest {
  date: string;
  reason?: string | null;
  amountBdt?: number | null;
}

export interface UpdateBillTermsRequest {
  partyName: string;
  partyPhone?: string | null;
  dueDate?: string | null;
  notes?: string | null;
}

export type TradeBillKind = 'Payable' | 'Receivable';
export type TradeBillStatus = 'Open' | 'PartiallyPaid' | 'Paid' | 'Overpaid' | 'Cancelled';

export interface TradeBillPayment {
  id: string;
  billId: string;
  paymentDate: string;
  amountBdt: number;
  method: string;
  reference?: string | null;
  notes?: string | null;
}

export interface TradeBill {
  id: string;
  farmId: string;
  kind: TradeBillKind;
  status: TradeBillStatus;
  partyName: string;
  partyPhone?: string | null;
  supplierId?: string | null;
  sourceDocumentId: string;
  category: string;
  categoryName: string;
  sourceModule?: string | null;
  referenceNo: string;
  description: string;
  notes?: string | null;
  billDate: string;
  dueDate?: string | null;
  daysOverdue: number;
  agingBucket: string;
  amountBdt: number;
  paidBdt: number;
  adjustedBdt: number;
  outstandingBdt: number;
  payments: TradeBillPayment[];
}

export interface AgingBucket {
  key: string;
  label: string;
  count: number;
  amountBdt: number;
}

export interface PartyBalance {
  partyName: string;
  partyPhone?: string | null;
  openBills: number;
  billedBdt: number;
  paidBdt: number;
  outstandingBdt: number;
  overdueBdt: number;
  oldestDueDate?: string | null;
}

export interface TradeCreditSide {
  kind: TradeBillKind;
  outstandingBdt: number;
  overdueBdt: number;
  dueThisWeekBdt: number;
  openBills: number;
  ledgerBalanceBdt: number;
  isReconciled: boolean;
  aging: AgingBucket[];
  parties: PartyBalance[];
}

export interface TradeCreditOverview {
  farmId: string;
  asOf: string;
  payables: TradeCreditSide;
  receivables: TradeCreditSide;
  bills: TradeBill[];
}

export interface TradeCreditMovement {
  receivablesOpeningBdt: number;
  creditSalesBdt: number;
  collectedBdt: number;
  badDebtsBdt: number;
  receivablesClosingBdt: number;
  payablesOpeningBdt: number;
  creditPurchasesBdt: number;
  paidToSuppliersBdt: number;
  returnsAndWaiversBdt: number;
  payablesClosingBdt: number;
}

/** How a payment was made — all are Cash & Bank in the books; kept for the receipt trail. */
export const PAYMENT_METHODS = ['Cash', 'Bank', 'bKash', 'Nagad', 'Rocket', 'Cheque'];


// ── Money accounts (cash in hand, bank, mobile wallet) ──────────────────────

export type MoneyAccountKind = 'Cash' | 'Bank' | 'MobileWallet';

export const MONEY_ACCOUNT_KINDS: { value: MoneyAccountKind; label: string; icon: string }[] = [
  { value: 'Cash', label: 'Cash', icon: 'payments' },
  { value: 'Bank', label: 'Bank account', icon: 'account_balance' },
  { value: 'MobileWallet', label: 'Mobile wallet (bKash, Nagad…)', icon: 'smartphone' },
];

export interface MoneyAccount {
  id: string;
  farmId: string;
  name: string;
  kind: MoneyAccountKind | string;
  providerName?: string | null;
  accountNumber?: string | null;
  branch?: string | null;
  notes?: string | null;
  isDefault: boolean;
  isActive: boolean;
  balanceBdt: number;
}

export interface SaveMoneyAccountRequest {
  name: string;
  kind: MoneyAccountKind | string;
  providerName?: string | null;
  accountNumber?: string | null;
  branch?: string | null;
  notes?: string | null;
}

export interface TransferBetweenAccountsRequest {
  fromAccountId: string;
  toAccountId: string;
  amountBdt: number;
  transferDate: string;
  referenceId?: string | null;
  notes?: string | null;
}

export interface MoneyTransfer {
  outTransactionId: string;
  inTransactionId: string;
  fromAccountId: string;
  toAccountId: string;
  amountBdt: number;
  transferDate: string;
  referenceId: string;
}

export interface MoneyAccountLine {
  transactionId: string;
  date: string;
  voucherNo: string;
  description: string;
  category: string;
  categoryName: string;
  categoryNameBn: string;
  inBdt: number;
  outBdt: number;
  balanceBdt: number;
  isTransfer: boolean;
  counterAccountName?: string | null;
  linkedTransactionId?: string | null;
  sourceModule?: string | null;
}

export interface MoneyAccountStatement {
  account: MoneyAccount;
  fromDate: string;
  toDate: string;
  openingBdt: number;
  totalInBdt: number;
  totalOutBdt: number;
  closingBdt: number;
  lines: MoneyAccountLine[];
}

export interface MoneyAccountBreakdown {
  id: string;
  name: string;
  kind: MoneyAccountKind | string;
  isDefault: boolean;
  balanceBdt: number;
}

/** The default account's stored name; shown translated ("হাতে নগদ"). */
export const DEFAULT_MONEY_ACCOUNT_NAME = 'Cash in hand';

/** An account's display name: the default account's English name goes through the dictionary. */
export function moneyAccountName(name: string | null | undefined): string {
  if (!name) return '';
  return name === DEFAULT_MONEY_ACCOUNT_NAME ? translate(name) : name;
}

/**
 * The server describes each register with a count baked in ("Herd: 23 animal(s) on farm at
 * purchase cost"), so a plain dictionary lookup can't match it. Pull the number out, translate the
 * pattern, put the number back.
 */
export function registerSourceText(text: string | null | undefined): string {
  if (!text) return '';
  const m = /\d+/.exec(text);
  if (!m) return translate(text);
  const pattern = text.slice(0, m.index) + '{n}' + text.slice(m.index + m[0].length);
  const translated = translate(pattern, { n: m[0] });
  return translated === pattern.replace('{n}', m[0]) ? text : translated;
}

export function moneyAccountIcon(kind: string | null | undefined): string {
  return kind === 'Bank' ? 'account_balance' : kind === 'MobileWallet' ? 'smartphone' : 'payments';
}

/**
 * Where the money for a form came from (or went to). One of: a money account, on credit (বাকি),
 * or paid directly by a partner (books that partner's capital contribution as well).
 */
export type PaidFromMode = 'account' | 'credit' | 'partner';

export interface PaidFromPayload {
  moneyAccountId: string | null;
  paidByPartnerId: string | null;
  credit: CreditTerms | null;
}

// ── Partners (investors presented as partners; profit split by capital) ─────

export interface PartnerSummary {
  id: string;
  name: string;
  phone?: string | null;
  isActive: boolean;
  joinedOn: string;
  depositedBdt: number;
  withdrawnBdt: number;
  capitalBdt: number;
  sharePercent: number;
  profitAllocatedBdt: number;
  profitPaidBdt: number;
  profitDueBdt: number;
  equityBdt: number;
}

export interface PartnersOverview {
  totalCapitalBdt: number;
  netAssetsBdt: number;
  profitAllocatedBdt: number;
  profitPaidBdt: number;
  partners: PartnerSummary[];
}

export interface PartnerAllocationLine {
  partnerId: string;
  name: string;
  capitalDays: number;
  averageCapitalBdt: number;
  sharePercent: number;
  allocatedBdt: number;
}

export interface PartnerProfitAllocation {
  fromDate: string;
  toDate: string;
  days: number;
  netProfitBdt: number;
  charityPercent: number;
  charityBdt: number;
  distributableProfitBdt: number;
  lines: PartnerAllocationLine[];
}

export interface PartnerLedgerLine {
  id?: string | null;
  date: string;
  type: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution' | 'ProfitAllocation' | string;
  amountBdt: number;
  capitalAfterBdt: number;
  moneyAccountName?: string | null;
  referenceId?: string | null;
  notes?: string | null;
}

export interface NetAssetLine {
  key: string;
  name: string;
  nameBn: string;
  farmBdt: number;
  partnerBdt: number;
}

export interface PartnerStatement {
  partner: PartnerSummary;
  fromDate: string;
  toDate: string;
  openingCapitalBdt: number;
  closingCapitalBdt: number;
  lines: PartnerLedgerLine[];
  allocation: PartnerProfitAllocation;
  netAssets: NetAssetLine[];
  netAssetsBdt: number;
  partnerNetAssetsBdt: number;
}

export interface PartnerPart {
  partnerId: string;
  name: string;
  sharePercent: number;
  amountBdt: number;
}

export interface WhoseMoneyRow {
  transactionId: string;
  date: string;
  description: string;
  category: string;
  categoryName: string;
  categoryNameBn: string;
  amountBdt: number;
  animalId?: string | null;
  parts: PartnerPart[];
}

export interface WhoseMoney {
  partners: { id: string; name: string }[];
  rows: WhoseMoneyRow[];
  totalBdt: number;
  totals: PartnerPart[];
}
