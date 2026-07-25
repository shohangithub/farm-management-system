import { Routes } from '@angular/router';
import { OrganizationListComponent } from './organization-list/organization-list.component';
import { OrganizationFormComponent } from './organization-form/organization-form.component';

export const ORGANIZATION_ROUTES: Routes = [
  { path: '', component: OrganizationListComponent },
  { path: 'new', component: OrganizationFormComponent },
  { path: 'edit/:id', component: OrganizationFormComponent },
  { path: 'detail/:id', loadComponent: () => import('./organization-detail/organization-detail.component').then(c => c.OrganizationDetailComponent) },
  { path: ':orgId/branches', loadComponent: () => import('./branch-list/branch-list.component').then(c => c.BranchListComponent) },
  { path: ':orgId/branches/new', loadComponent: () => import('./branch-form/branch-form.component').then(c => c.BranchFormComponent) },
  { path: ':orgId/branches/edit/:branchId', loadComponent: () => import('./branch-form/branch-form.component').then(c => c.BranchFormComponent) },
  { path: ':orgId/branches/detail/:branchId', loadComponent: () => import('./branch-detail/branch-detail.component').then(c => c.BranchDetailComponent) },
  { path: 'branches/:branchId/farms', loadComponent: () => import('../farms/farm-list/farm-list.component').then(c => c.FarmListComponent) },
  { path: 'branches/:branchId/farms/new', loadComponent: () => import('../farms/farm-form/farm-form.component').then(c => c.FarmFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/edit', loadComponent: () => import('../farms/farm-form/farm-form.component').then(c => c.FarmFormComponent) },
  { path: 'branches/:branchId/farms/:farmId', loadComponent: () => import('../farms/farm-detail/farm-detail.component').then(c => c.FarmDetailComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds', loadComponent: () => import('../farms/shed-list/shed-list.component').then(c => c.ShedListComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/new', loadComponent: () => import('../farms/shed-form/shed-form.component').then(c => c.ShedFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId/edit', loadComponent: () => import('../farms/shed-form/shed-form.component').then(c => c.ShedFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId', loadComponent: () => import('../farms/shed-detail/shed-detail.component').then(c => c.ShedDetailComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId/pens', loadComponent: () => import('../farms/pen-list/pen-list.component').then(c => c.PenListComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId/pens/new', loadComponent: () => import('../farms/pen-form/pen-form.component').then(c => c.PenFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId/pens/:penId/edit', loadComponent: () => import('../farms/pen-form/pen-form.component').then(c => c.PenFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/sheds/:shedId/pens/:penId', loadComponent: () => import('../farms/pen-detail/pen-detail.component').then(c => c.PenDetailComponent) }
];
