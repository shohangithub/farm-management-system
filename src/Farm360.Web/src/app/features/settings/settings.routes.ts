import { Routes } from '@angular/router';
import { MasterDataComponent } from './master-data/master-data.component';
import { SettingsHubComponent } from './settings-hub.component';

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
  }
];
