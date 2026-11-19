import { Routes } from '@angular/router';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';
import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { Permissions } from './core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full',
      },
      {
        path: 'livestock',
        loadChildren: () =>
          import('./features/livestock/livestock.routes').then(m => m.livestockRoutes),
      },
      {
        path: 'dashboard',
        canActivate: [permissionGuard],
        data: { permission: Permissions.Dashboard.View },
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES),
      },
      {
        path: 'health',
        loadChildren: () =>
          import('./features/health/health.routes').then(m => m.HEALTH_ROUTES),
      },
      {
        path: 'feeding',
        loadChildren: () =>
          import('./features/feeding/feeding.routes').then(m => m.FEEDING_ROUTES),
      },
      {
        path: 'inventory',
        loadChildren: () =>
          import('./features/inventory/inventory.routes').then(m => m.INVENTORY_ROUTES),
      },
      {
        path: 'finance',
        canActivate: [permissionGuard],
        data: { permission: Permissions.Finance.View },
        loadChildren: () =>
          import('./features/finance/finance.routes').then(m => m.FINANCE_ROUTES),
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports.routes').then(m => m.reportsRoutes),
      },
      {
        path: 'organizations',
        loadChildren: () =>
          import('./features/organizations/organizations.routes').then(m => m.ORGANIZATION_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () =>
          import('./features/settings/settings.routes').then(m => m.SETTINGS_ROUTES),
      },
      {
        path: 'access-denied',
        loadComponent: () =>
          import('./shared/components/access-denied/access-denied.component').then(m => m.AccessDeniedComponent),
        title: 'Access denied - Farm360 AI',
      },
      {
        path: 'billing',
        redirectTo: 'settings/billing',
        pathMatch: 'full',
      },
      {
        path: 'subscription',
        redirectTo: 'settings/billing',
        pathMatch: 'full',
      }
    ]
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES),
  },
  {
    // Short public link for invitations (query params such as ?token= are kept).
    path: 'accept-invitation',
    redirectTo: 'auth/accept-invitation',
    pathMatch: 'full',
  },
  {
    path: 'login',
    redirectTo: 'auth/login',
    pathMatch: 'full',
  },
  {
    path: 'register',
    redirectTo: 'auth/register',
    pathMatch: 'full',
  },
  {
    path: 'signup',
    redirectTo: 'auth/register',
    pathMatch: 'full',
  },
  {
    path: '**',
    redirectTo: 'dashboard',
  },
];
