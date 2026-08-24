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
    path: 'current-stock',
    loadComponent: () => import('./pages/current-stock/current-stock.component').then(m => m.CurrentStockComponent)
  },
  {
    path: 'transactions',
    loadComponent: () => import('./pages/stock-ledger/stock-ledger.component').then(m => m.StockLedgerComponent)
  },
  {
    path: 'suppliers',
    loadComponent: () => import('./pages/supplier-list/supplier-list.component').then(m => m.SupplierListComponent)
  },
  {
    path: 'purchase-orders',
    loadComponent: () => import('./components/purchase-order-list/purchase-order-list').then(m => m.PurchaseOrderList)
  },
  {
    path: 'purchase-orders/new',
    loadComponent: () => import('./components/purchase-order-form/purchase-order-form').then(m => m.PurchaseOrderForm)
  },
  {
    path: 'purchase-orders/:id',
    loadComponent: () => import('./components/purchase-order-detail/purchase-order-detail').then(m => m.PurchaseOrderDetail)
  },
  {
    path: 'reports/movement',
    loadComponent: () => import('./pages/inventory-movement-report/inventory-movement-report').then(m => m.InventoryMovementReport)
  }
];
