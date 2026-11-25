import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn, HttpContextToken } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';

/**
 * Set to true on a request (`new HttpContext().set(SKIP_GLOBAL_ERROR_TOAST, true)`) when the caller
 * shows its own message for offline / server errors.
 */
export const SKIP_GLOBAL_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * Set to true on public (anonymous) calls that must not carry the signed-in user's bearer token,
 * e.g. accepting an invitation to another organisation. 401s on such calls never trigger a refresh.
 */
export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);

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
  const notify = inject(NotificationService);
  const token = authService.accessToken;

  // Prepend API URL from environment if configured (for production cross-domain hosting)
  if (environment.apiUrl && req.url.startsWith('/api/')) {
    req = req.clone({ url: `${environment.apiUrl}${req.url}` });
  }

  const skipAuth = req.context.get(SKIP_AUTH);
  if (token && !skipAuth && req.url.includes('/api/')) {
    req = addTokenHeader(req, token);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (skipAuth && error.status === 401) {
        // Public call: never refresh or sign out the signed-in session; the caller shows the error.
      } else if (error.status === 401 && !req.url.includes('/api/v1/auth/login') && !req.url.includes('/api/v1/auth/refresh')) {
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
        // The demo account is read-only everywhere: say so instead of a generic "access denied".
        const demoReadOnly = (error.error as { type?: string } | null)?.type === 'https://farm360.ai/errors/demo-read-only';
        notify.reportGlobalError(demoReadOnly
          ? "You're exploring a demo farm with sample data. Nothing can be changed."
          : 'Access denied: you do not have permission to perform this action.');
      } else if ((error.status === 0 || error.status >= 500) && !req.context.get(SKIP_GLOBAL_ERROR_TOAST)) {
        const offline = error.status === 0 || (typeof navigator !== 'undefined' && navigator.onLine === false);
        notify.reportGlobalError(offline
          ? 'You appear to be offline. Check your internet connection and try again.'
          : 'The server had a problem processing your request. Please try again in a moment.');
      }
      return throwError(() => error);
    })
  );
};
