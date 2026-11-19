import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { Observable, of } from 'rxjs';
import { ConfirmService } from '../services/confirm.service';

/**
 * Implemented by full-page forms. Return true when the user has edits that would be lost
 * (typically `this.form.dirty && !this.saved`).
 */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/**
 * Route guard: `canDeactivate: [unsavedChangesGuard]` on a route whose component implements
 * HasUnsavedChanges. Asks before leaving a page with unsaved edits.
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component): Observable<boolean> => {
  if (!component?.hasUnsavedChanges || !component.hasUnsavedChanges()) return of(true);
  return inject(ConfirmService).confirm({
    title: 'Discard unsaved changes?',
    message: 'You have changes on this page that have not been saved. If you leave now they will be lost.',
    confirmText: 'Discard',
    cancelText: 'Stay on page',
    destructive: true
  });
};
