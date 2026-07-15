import { Routes } from '@angular/router';

export const routes: Routes = [
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
    path: '**',
    redirectTo: 'livestock',
  },
];
