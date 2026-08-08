import { Routes } from '@angular/router';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/finance-ledger/finance-ledger.component').then(m => m.FinanceLedgerComponent),
    title: 'Finance Ledger - Farm360 AI'
  },
  {
    path: 'analytics',
    loadComponent: () => import('./components/finance-dashboard/finance-dashboard').then(m => m.FinanceDashboardComponent),
    title: 'Finance Analytics - Farm360 AI'
  }
];
