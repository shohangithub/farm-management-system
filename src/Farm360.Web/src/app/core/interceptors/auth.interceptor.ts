import { HttpInterceptorFn } from '@angular/common/http';

/**
 * JWT auth interceptor — attaches Bearer token from localStorage to all API calls.
 * Production: replace localStorage with a secure AuthService (HttpOnly cookie or memory store).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('farm360_access_token');

  if (token && req.url.startsWith('/api/')) {
    return next(req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    }));
  }

  return next(req);
};
