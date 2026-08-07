import { Routes } from '@angular/router';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/finance-ledger/finance-ledger.component').then(m => m.FinanceLedgerComponent),
    title: 'Finance Ledger - Farm360 AI'
  }
];
