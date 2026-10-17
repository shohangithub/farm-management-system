import { Routes } from '@angular/router';

/**
 * Feeding, in the order the work happens: Today → Who Eats What → setup (Feed Charts, Recipes)
 * → records. Old addresses redirect so bookmarks and report links keep working.
 */
export const FEEDING_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'today',
    pathMatch: 'full'
  },
  {
    path: 'today',
    loadComponent: () => import('./pages/today-feeding-dashboard/today-feeding-dashboard.component').then(m => m.TodayFeedingDashboardComponent),
    title: 'Today\'s Feeding — Farm360 AI'
  },
  {
    path: 'board',
    loadComponent: () => import('./pages/feeding-board/feeding-board.component').then(m => m.FeedingBoardComponent),
    title: 'Who Eats What — Farm360 AI'
  },
  {
    // What to buy for the next 7 / 10 / 15 / 30 days
    path: 'planner',
    loadComponent: () => import('./pages/feed-planner/feed-planner.component').then(m => m.FeedPlannerComponent),
    title: 'Feed Purchase Plan — Farm360 AI'
  },
  {
    // Protein, energy & dry matter one animal ate vs. its needs, and how it grew
    path: 'nutrition',
    loadComponent: () => import('./pages/nutrition-growth/nutrition-growth.component').then(m => m.NutritionGrowthComponent),
    title: 'Nutrition & Growth — Farm360 AI'
  },
  {
    path: 'rules',
    loadComponent: () => import('./pages/rule-set-list/rule-set-list.component').then(m => m.RuleSetListComponent),
    title: 'Feed Charts — Farm360 AI'
  },
  {
    path: 'recipes',
    loadComponent: () => import('./pages/recipes/recipes.component').then(m => m.RecipesComponent),
    title: 'Recipes — Farm360 AI'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/feeding-dashboard/feeding-dashboard.component').then(m => m.FeedingDashboardComponent),
    title: 'Feeding Overview — Farm360 AI'
  },
  {
    path: 'logs',
    loadComponent: () => import('./pages/consumption-log/consumption-log.component').then(m => m.ConsumptionLogComponent),
    title: 'Feed Records — Farm360 AI'
  },
  {
    path: 'reconciliations',
    loadComponent: () => import('./pages/reconciliation-list/reconciliation-list.component').then(m => m.ReconciliationListComponent),
    title: 'Feeding Reconciliations — Farm360 AI'
  },
  {
    // Every plan ever assigned, including stopped ones (the board shows only today's).
    path: 'plans',
    loadComponent: () => import('./pages/animal-feeding-plan-list/animal-feeding-plan-list.component').then(m => m.AnimalFeedingPlanListComponent),
    title: 'Feeding Plan History — Farm360 AI'
  },

  // ── Retired / merged pages ───────────────────────────────────────────────
  { path: 'formulas', redirectTo: 'recipes', pathMatch: 'full' },
  { path: 'ingredients', redirectTo: 'recipes?tab=ingredients', pathMatch: 'full' },
  // Schedules are converted to shed/batch rations from the Who Eats What page.
  { path: 'schedules', redirectTo: 'board', pathMatch: 'full' }
];
