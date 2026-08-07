import { Routes } from '@angular/router';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/executive-dashboard/executive-dashboard.component').then(m => m.ExecutiveDashboardComponent)
  }
];
