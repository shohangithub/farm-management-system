import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { combineLatest, filter, map, take } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Route guard for permission-protected screens. Reads `data.permission` (a code, or an array of
 * codes of which ANY grants access). Waits for the session and the user profile to load, then
 * redirects to the friendly /access-denied page instead of rendering a screen whose API calls
 * would all fail with 403.
 *
 * Usage: `{ path: 'finance', canActivate: [permissionGuard], data: { permission: 'finance.view' } }`
 */
export const permissionGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const required = route.data?.['permission'] as string | string[] | undefined;
  const codes = Array.isArray(required) ? required : required ? [required] : [];
  if (codes.length === 0) {
    return true;
  }

  return combineLatest([authService.isInitialized$, authService.currentUser$]).pipe(
    filter(([initialized, user]) => initialized && (!!user || !authService.isAuthenticated)),
    take(1),
    map(() => {
      if (!authService.isAuthenticated) {
        return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      }
      return authService.hasAnyPermission(codes)
        ? true
        : router.createUrlTree(['/access-denied'], { queryParams: { from: state.url } });
    })
  );
};
