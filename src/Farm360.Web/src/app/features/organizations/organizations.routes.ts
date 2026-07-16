import { Routes } from '@angular/router';
import { OrganizationListComponent } from './organization-list/organization-list.component';
import { OrganizationFormComponent } from './organization-form/organization-form.component';

export const ORGANIZATION_ROUTES: Routes = [
  { path: '', component: OrganizationListComponent },
  { path: 'new', component: OrganizationFormComponent },
  { path: 'edit/:id', component: OrganizationFormComponent },
  { path: ':orgId/branches', loadComponent: () => import('./branch-list/branch-list.component').then(c => c.BranchListComponent) },
  { path: ':orgId/branches/new', loadComponent: () => import('./branch-form/branch-form.component').then(c => c.BranchFormComponent) },
  { path: ':orgId/branches/edit/:branchId', loadComponent: () => import('./branch-form/branch-form.component').then(c => c.BranchFormComponent) },
  { path: ':orgId/branches/detail/:branchId', loadComponent: () => import('./branch-detail/branch-detail.component').then(c => c.BranchDetailComponent) },
  { path: 'branches/:branchId/farms', loadComponent: () => import('../farms/farm-list/farm-list.component').then(c => c.FarmListComponent) },
  { path: 'branches/:branchId/farms/new', loadComponent: () => import('../farms/farm-form/farm-form.component').then(c => c.FarmFormComponent) },
  { path: 'branches/:branchId/farms/:farmId/edit', loadComponent: () => import('../farms/farm-form/farm-form.component').then(c => c.FarmFormComponent) },
  { path: 'branches/:branchId/farms/:farmId', loadComponent: () => import('../farms/farm-detail/farm-detail.component').then(c => c.FarmDetailComponent) }
];
