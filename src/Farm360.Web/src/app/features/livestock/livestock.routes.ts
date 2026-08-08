import { Routes } from '@angular/router';

export const livestockRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./pages/animal-list/animal-list.component').then(m => m.AnimalListComponent),
    title: 'Livestock — Farm360',
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./pages/animal-register/animal-register.component').then(m => m.AnimalRegisterComponent),
    title: 'Register Animal — Farm360',
  },
  {
    path: 'batches',
    loadComponent: () =>
      import('./pages/batch-list/batch-list').then(m => m.BatchList),
    title: 'Batches — Farm360',
  },
  {
    path: 'batches/:id',
    loadComponent: () =>
      import('./pages/batch-detail/batch-detail').then(m => m.BatchDetail),
    title: 'Batch Detail — Farm360',
  },
  {
    path: 'breeds',
    loadComponent: () =>
      import('./pages/breed-list/breed-list').then(m => m.BreedList),
    title: 'Breeds — Farm360',
  },
  {
    path: 'breeding-analytics',
    loadComponent: () =>
      import('./components/breeding-dashboard/breeding-dashboard').then(m => m.BreedingDashboardComponent),
    title: 'Breeding Analytics — Farm360',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/animal-detail/animal-detail.component').then(m => m.AnimalDetailComponent),
    title: 'Animal Detail — Farm360',
  },
];
