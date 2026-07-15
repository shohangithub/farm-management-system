import { Routes } from '@angular/router';

export const HEALTH_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'vaccinations',
    pathMatch: 'full'
  },
  {
    path: 'vaccinations',
    loadComponent: () => import('./pages/vaccination-due-list/vaccination-due-list.component').then(c => c.VaccinationDueListComponent),
    title: 'Due Vaccinations - Farm360'
  },
  {
    path: 'incidents',
    loadComponent: () => import('./pages/report-incident/report-incident.component').then(c => c.ReportIncidentComponent),
    title: 'Disease Incidents - Farm360'
  }
];
