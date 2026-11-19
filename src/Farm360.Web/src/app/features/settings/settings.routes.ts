import { Routes } from '@angular/router';
import { unsavedChangesGuard } from '../../shared/utils/unsaved-changes.guard';
import { MasterDataComponent } from './master-data/master-data.component';
import { SettingsHubComponent } from './settings-hub.component';
import { permissionGuard } from '../../core/guards/permission.guard';
import { Permissions } from '../../core/auth/permissions';

export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    component: SettingsHubComponent,
  },
  {
    path: 'master-data',
    component: MasterDataComponent,
  },
  {
    path: 'profile',
    loadComponent: () =>
      import('./profile/profile.component').then(m => m.ProfileComponent),
    canDeactivate: [unsavedChangesGuard],
  },
  {
    // Settings → Farm rules: per-organisation valuation prices and allocation methods.
    path: 'farm-rules',
    loadComponent: () =>
      import('./business-rules/business-rules.component').then(m => m.BusinessRulesComponent),
    canDeactivate: [unsavedChangesGuard],
  },
  {
    // Settings → Team: members, roles/branches and invitations.
    path: 'team',
    canActivate: [permissionGuard],
    data: { permission: Permissions.Users.View },
    loadComponent: () =>
      import('./team/team.component').then(m => m.TeamComponent),
  },
  {
    path: 'billing',
    loadComponent: () =>
      import('./billing/billing.component').then(m => m.BillingComponent),
  }
];
