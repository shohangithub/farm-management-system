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
    title: 'Feed Rations — Farm360 AI'
  },
  {
    path: 'schedules',
    loadComponent: () => import('./pages/feeding-schedule-list/feeding-schedule-list.component').then(m => m.FeedingScheduleListComponent),
    title: 'Feeding Schedules — Farm360 AI'
  },
  {
    path: 'logs',
    loadComponent: () => import('./pages/consumption-log/consumption-log.component').then(m => m.ConsumptionLogComponent),
    title: 'Daily Feeding Records — Farm360 AI'
  },
  {
    path: 'rules',
    loadComponent: () => import('./pages/rule-set-list/rule-set-list.component').then(m => m.RuleSetListComponent),
    title: 'Feeding Rule Sets — Farm360 AI'
  },
  {
    path: 'plans',
    loadComponent: () => import('./pages/animal-feeding-plan-list/animal-feeding-plan-list.component').then(m => m.AnimalFeedingPlanListComponent),
    title: 'Animal Feeding Plans — Farm360 AI'
  },
  {
    path: 'today',
    loadComponent: () => import('./pages/today-feeding-dashboard/today-feeding-dashboard.component').then(m => m.TodayFeedingDashboardComponent),
    title: 'Today\'s Feeding Workflow — Farm360 AI'
  },
  {
    path: 'reconciliations',
    loadComponent: () => import('./pages/reconciliation-list/reconciliation-list.component').then(m => m.ReconciliationListComponent),
    title: 'Feeding Reconciliations — Farm360 AI'
  }
];
