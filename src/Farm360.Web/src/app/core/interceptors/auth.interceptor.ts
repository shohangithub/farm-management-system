import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';

// ── Module-level refresh lock ─────────────────────────────────────────────────
// isRefreshing prevents concurrent refresh attempts when multiple 401s arrive.
// refreshTokenSubject queues subsequent requests until the new token is ready.
// M6 Fix: These are reset when the session ends to prevent stale state on re-login.
let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

function addTokenHeader(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}

function resetRefreshState(): void {
  isRefreshing = false;
  refreshTokenSubject.next(null);
}

import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const authService = inject(AuthService);
  const snackBar = inject(MatSnackBar);
  const token = authService.accessToken;

  // Prepend API URL from environment if configured (for production cross-domain hosting)
  if (environment.apiUrl && req.url.startsWith('/api/')) {
    req = req.clone({ url: `${environment.apiUrl}${req.url}` });
  }

  if (token && req.url.includes('/api/')) {
    req = addTokenHeader(req, token);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/api/v1/auth/login') && !req.url.includes('/api/v1/auth/refresh')) {
        if (!isRefreshing) {
          isRefreshing = true;
          refreshTokenSubject.next(null);

          return authService.refresh().pipe(
            switchMap((response) => {
              isRefreshing = false;
              refreshTokenSubject.next(response.accessToken);
              return next(addTokenHeader(req, response.accessToken));
            }),
            catchError((refreshErr) => {
              // M6: Reset state so future login sessions start clean
              resetRefreshState();
              authService.logout();
              return throwError(() => refreshErr);
            })
          );
        } else {
          // Another refresh is in progress — queue this request until token is ready
          return refreshTokenSubject.pipe(
            filter((t): t is string => t !== null),
            take(1),
            switchMap((newToken) => next(addTokenHeader(req, newToken)))
          );
        }
      } else if (error.status === 401) {
        // 401 on login or refresh — session is unrecoverable
        resetRefreshState();
        authService.logout();
      } else if (error.status === 403) {
        snackBar.open('Access Denied: You do not have permission to perform this action.', 'Close', {
          duration: 5000,
          panelClass: ['error-snackbar']
        });
      }
      return throwError(() => error);
    })
  );
};
