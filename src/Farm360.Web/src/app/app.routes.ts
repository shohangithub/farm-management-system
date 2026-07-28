import { Routes } from '@angular/router';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: 'livestock',
        pathMatch: 'full',
      },
      {
        path: 'livestock',
        loadChildren: () =>
          import('./features/livestock/livestock.routes').then(m => m.livestockRoutes),
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
        path: 'organizations',
        loadChildren: () =>
          import('./features/organizations/organizations.routes').then(m => m.ORGANIZATION_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () =>
          import('./features/settings/settings.routes').then(m => m.SETTINGS_ROUTES),
      }
    ]
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES),
  },
  {
    path: 'login',
    redirectTo: 'auth/login',
    pathMatch: 'full',
  },
  {
    path: '**',
    redirectTo: 'livestock',
  },
];
