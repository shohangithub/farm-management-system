import { Routes } from '@angular/router';
import { MasterDataComponent } from './master-data/master-data.component';

export const SETTINGS_ROUTES: Routes = [
  {
    path: 'master-data',
    component: MasterDataComponent,
  },
  {
    path: '',
    redirectTo: 'master-data',
    pathMatch: 'full'
  }
];
