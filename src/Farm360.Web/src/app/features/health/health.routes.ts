import { Routes } from '@angular/router';

export const HEALTH_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/health-dashboard/health-dashboard.component').then(c => c.HealthDashboardComponent),
    title: 'Health Dashboard - Farm360'
  },
  {
    path: 'vaccinations',
    loadComponent: () => import('./pages/vaccination-due-list/vaccination-due-list.component').then(c => c.VaccinationDueListComponent),
    title: 'Due Vaccinations - Farm360'
  },
  {
    path: 'vaccination-protocols',
    loadComponent: () => import('./pages/vaccination-protocol-list/vaccination-protocol-list.component').then(c => c.VaccinationProtocolListComponent),
    title: 'Vaccination Protocols - Farm360'
  },
  {
    path: 'vaccination-protocols/:id',
    loadComponent: () => import('./pages/vaccination-protocol-detail/vaccination-protocol-detail.component').then(c => c.VaccinationProtocolDetailComponent),
    title: 'Protocol Details - Farm360'
  },
  {
    path: 'treatments',
    loadComponent: () => import('./pages/treatment-list/treatment-list.component').then(c => c.TreatmentListComponent),
    title: 'Medical Treatments - Farm360'
  },
  {
    path: 'incidents',
    loadComponent: () => import('./pages/incident-list/incident-list.component').then(c => c.IncidentListComponent),
    title: 'Disease Incidents - Farm360'
  },
  {
    path: 'incidents/:id',
    loadComponent: () => import('./pages/incident-detail/incident-detail.component').then(c => c.IncidentDetailComponent),
    title: 'Incident Details - Farm360'
  },
  {
    path: 'deworming-calendar',
    loadComponent: () => import('./pages/deworming-calendar/deworming-calendar.component').then(c => c.DewormingCalendarComponent),
    title: 'Deworming Calendar - Farm360'
  },
  {
    path: 'milk-withdrawal',
    loadComponent: () => import('./pages/milk-withdrawal/milk-withdrawal.component').then(c => c.MilkWithdrawalComponent),
    title: 'Milk Withdrawal - Farm360'
  },
  {
    path: 'mortality-records',
    loadComponent: () => import('./pages/mortality-list/mortality-list.component').then(c => c.MortalityListComponent),
    title: 'Mortality Records - Farm360'
  },
  {
    path: 'vet-visits',
    loadComponent: () => import('./pages/vet-visit-list/vet-visit-list.component').then(c => c.VetVisitListComponent),
    title: 'Vet Visits - Farm360'
  }
];
