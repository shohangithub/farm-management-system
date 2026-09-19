import { Routes } from '@angular/router';

export const reportsRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./pages/report-center/report-center.component').then(m => m.ReportCenterComponent),
    title: 'Reports — Farm360',
  },
  {
    // Report keys contain dots ("feeding.animal-feeding"), which a single path segment handles.
    path: ':key',
    loadComponent: () =>
      import('./pages/report-viewer/report-viewer.component').then(m => m.ReportViewerComponent),
    title: 'Report — Farm360',
  },
];
