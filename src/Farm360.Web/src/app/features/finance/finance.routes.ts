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
  }
];
