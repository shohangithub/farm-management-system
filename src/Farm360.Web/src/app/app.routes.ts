import { Routes } from '@angular/router';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
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
    path: '**',
    redirectTo: 'livestock',
  },
];
