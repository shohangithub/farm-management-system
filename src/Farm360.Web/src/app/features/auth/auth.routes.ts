import { Routes } from '@angular/router';

export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./register/register.component').then(m => m.RegisterComponent),
  },
  {
    // Public: opened from the invitation SMS/email link (?token=…).
    path: 'accept-invitation',
    loadComponent: () =>
      import('./accept-invitation/accept-invitation.component').then(m => m.AcceptInvitationComponent),
    title: 'Accept invitation - Farm360 AI',
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent),
    title: 'Forgot password - Farm360 AI',
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./reset-password/reset-password.component').then(m => m.ResetPasswordComponent),
    title: 'Reset password - Farm360 AI',
  },
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  }
];
