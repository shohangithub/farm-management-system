import { Routes } from '@angular/router';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/finance-dashboard/finance-dashboard').then(m => m.FinanceDashboardComponent),
    title: 'Finance Dashboard - Farm360 AI'
  },
  {
    path: 'transactions',
    loadComponent: () => import('./pages/finance-ledger/finance-ledger.component').then(m => m.FinanceLedgerComponent),
    title: 'Finance Ledger - Farm360 AI'
  },
  {
    path: 'loans',
    loadComponent: () => import('./pages/loan-list/loan-list').then(m => m.LoanListComponent),
    title: 'Loans - Farm360 AI'
  },
  {
    path: 'investors',
    loadComponent: () => import('./pages/investor-list/investor-list').then(m => m.InvestorListComponent),
    title: 'Investors & Equity - Farm360 AI'
  },
  {
    path: 'investors/pnl',
    loadComponent: () => import('./pages/investor-pnl/investor-pnl').then(m => m.InvestorPnLComponent),
    title: 'Profit & Loss Sharing - Farm360 AI'
  },
  {
    path: 'shares',
    loadComponent: () => import('./pages/share-market/share-market.component').then(m => m.ShareMarketDashboardComponent),
    title: 'Farm Share Market - Farm360 AI'
  },
  {
    path: 'animal-ledger/:animalId',
    loadComponent: () => import('./pages/animal-cost-ledger/animal-cost-ledger').then(m => m.AnimalCostLedgerComponent),
    title: 'Animal Cost Ledger - Farm360 AI'
  },
  {
    path: 'reports/batch-pnl/:batchId',
    loadComponent: () => import('./pages/batch-pnl-report/batch-pnl-report').then(m => m.BatchPnlReportComponent),
    title: 'Batch P&L Report - Farm360 AI'
  },
  {
    path: 'reports/monthly-pnl',
    loadComponent: () => import('./pages/monthly-pnl-report/monthly-pnl-report').then(m => m.MonthlyPnlReportComponent),
    title: 'Monthly P&L Report - Farm360 AI'
  },
  {
    path: 'reports/trial-balance',
    loadComponent: () => import('./pages/trial-balance/trial-balance.component').then(m => m.TrialBalanceComponent),
    title: 'Trial Balance - Farm360 AI'
  },
  {
    path: 'reports/income-expense',
    loadComponent: () => import('./pages/income-expense-report/income-expense-report').then(m => m.IncomeExpenseReportComponent),
    title: 'Income & Expense Statement - Farm360 AI'
  },
  {
    path: 'reports/full-accounts',
    loadComponent: () => import('./pages/full-accounts-report/full-accounts-report').then(m => m.FullAccountsReportComponent),
    title: 'Full Accounts Statement - Farm360 AI'
  },
  {
    path: 'credit',
    loadComponent: () => import('./pages/trade-credit/trade-credit').then(m => m.TradeCreditComponent),
    title: 'Payables & Receivables - Farm360 AI'
  },
  {
    path: 'accounting-health',
    loadComponent: () => import('./pages/accounting-health/accounting-health').then(m => m.AccountingHealthComponent),
    title: 'Accounting Health - Farm360 AI'
  },
  {
    path: 'reports/balance-sheet',
    loadComponent: () => import('./pages/balance-sheet/balance-sheet.component').then(m => m.BalanceSheetComponent),
    title: 'Balance Sheet - Farm360 AI'
  },
  {
    path: 'reports/income-statement',
    loadComponent: () => import('./pages/income-statement/income-statement.component').then(m => m.IncomeStatementComponent),
    title: 'Profit & Loss Statement - Farm360 AI'
  },
  {
    path: 'fixed-assets',
    loadComponent: () => import('./pages/fixed-assets/fixed-assets.component').then(m => m.FixedAssetsComponent),
    title: 'Fixed Assets - Farm360 AI'
  },
  {
    path: 'accounts-setup',
    loadComponent: () => import('./pages/accounts-setup/accounts-setup.component').then(m => m.AccountsSetupComponent),
    title: 'Accounts Setup - Farm360 AI'
  }
];
