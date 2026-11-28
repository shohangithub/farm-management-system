import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';

/** Header the API uses for non-blocking warnings (e.g. "Bank has ৳40,000; this spends ৳60,000"). */
export const WARNINGS_HEADER = 'X-Farm360-Warnings';

/**
 * Shows the server's non-blocking warnings after a request succeeded. The save went through; the
 * warning only tells the user something to look at (an account spent below zero, for instance).
 * The header holds a URI-escaped JSON array of messages already in the user's language.
 */
export const warningsInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  return next(req).pipe(
    tap(event => {
      if (!(event instanceof HttpResponse)) return;
      const raw = event.headers.get(WARNINGS_HEADER);
      if (!raw) return;
      try {
        const messages = JSON.parse(decodeURIComponent(raw)) as unknown;
        if (Array.isArray(messages) && messages.length > 0) {
          notifications.warning(messages.map(String).join('\n'), { duration: 9000 });
        }
      } catch {
        // A malformed header must never break the request it came with.
      }
    })
  );
};
