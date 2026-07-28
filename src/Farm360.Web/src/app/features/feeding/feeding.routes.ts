import { Routes } from '@angular/router';

export const FEEDING_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/feeding-dashboard/feeding-dashboard.component').then(m => m.FeedingDashboardComponent),
    title: 'Smart Feeding Dashboard — Farm360 AI'
  },
  {
    path: 'ingredients',
    loadComponent: () => import('./pages/ingredient-list/ingredient-list.component').then(m => m.IngredientListComponent),
    title: 'Feed Ingredients Catalog — Farm360 AI'
  },
  {
    path: 'formulas',
    loadComponent: () => import('./pages/formula-list/formula-list.component').then(m => m.FormulaListComponent),
    title: 'Feed Formulas — Farm360 AI'
  },
  {
    path: 'schedules',
    loadComponent: () => import('./pages/feeding-schedule-list/feeding-schedule-list.component').then(m => m.FeedingScheduleListComponent),
    title: 'Feeding Schedules — Farm360 AI'
  },
  {
    path: 'logs',
    loadComponent: () => import('./pages/consumption-log/consumption-log.component').then(m => m.ConsumptionLogComponent),
    title: 'Daily Consumption Logs — Farm360 AI'
  }
];
