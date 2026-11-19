import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig, MatSnackBarRef, TextOnlySnackBar } from '@angular/material/snack-bar';

export type NotificationKind = 'success' | 'error' | 'warning' | 'info';

/** One action label and one set of durations for every toast in the app (errors stay longer). */
const DURATIONS: Record<NotificationKind, number> = {
  success: 3000,
  info: 4000,
  warning: 6000,
  error: 8000
};
const ACTION_LABEL = 'Dismiss';
const GLOBAL_ERROR_WINDOW_MS = 4000;

/**
 * Single entry point for snackbar messages. Styles live in styles.scss (`.snack-success`,
 * `.snack-error`, `.snack-warning`, `.snack-info`) and use the existing palette colours.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);
  private lastGlobalErrorAt = 0;
  private lastGlobalMessage = '';

  success(message: string, config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> {
    return this.show('success', message, config);
  }

  /**
   * Page-level error toast. Skipped when the HTTP interceptor has just shown a global
   * offline / server / access-denied message for the same failure, so users see one toast.
   */
  error(message: string, config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> | null {
    if (Date.now() - this.lastGlobalErrorAt < GLOBAL_ERROR_WINDOW_MS) return null;
    return this.show('error', message, config);
  }

  /** Used by the HTTP interceptor: one friendly message per burst of failing requests. */
  reportGlobalError(message: string): void {
    const now = Date.now();
    if (now - this.lastGlobalErrorAt < GLOBAL_ERROR_WINDOW_MS && message === this.lastGlobalMessage) return;
    this.lastGlobalErrorAt = now;
    this.lastGlobalMessage = message;
    this.show('error', message);
  }

  warning(message: string, config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> {
    return this.show('warning', message, config);
  }

  info(message: string, config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> {
    return this.show('info', message, config);
  }

  show(kind: NotificationKind, message: string, config?: MatSnackBarConfig): MatSnackBarRef<TextOnlySnackBar> {
    return this.snackBar.open(message, ACTION_LABEL, {
      duration: DURATIONS[kind],
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      politeness: kind === 'error' ? 'assertive' : 'polite',
      ...config,
      panelClass: ['farm-snack', `snack-${kind}`]
    });
  }
}
