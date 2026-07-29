import { Routes } from '@angular/router';

export const INVENTORY_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/inventory-dashboard/inventory-dashboard.component').then(m => m.InventoryDashboardComponent)
  },
  {
    path: 'items',
    loadComponent: () => import('./pages/inventory-item-list/inventory-item-list.component').then(m => m.InventoryItemListComponent)
  },
  {
    path: 'transactions',
    loadComponent: () => import('./pages/stock-ledger/stock-ledger.component').then(m => m.StockLedgerComponent)
  },
  {
    path: 'suppliers',
    loadComponent: () => import('./pages/supplier-list/supplier-list.component').then(m => m.SupplierListComponent)
  }
];
