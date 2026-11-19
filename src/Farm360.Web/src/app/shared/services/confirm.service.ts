import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, firstValueFrom, map } from 'rxjs';
import {
  ConfirmationDialogComponent,
  ConfirmationDialogData
} from '../components/confirmation-dialog/confirmation-dialog.component';

export interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Red confirm button for deletes / irreversible actions. */
  destructive?: boolean;
}

/** Replacement for window.confirm()/alert() built on the shared ConfirmationDialogComponent. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  /** Emits true when the user confirms, false otherwise (cancel, backdrop, Esc). */
  confirm(options: ConfirmOptions | string): Observable<boolean> {
    const opts: ConfirmOptions = typeof options === 'string' ? { message: options } : options;
    const data: ConfirmationDialogData = {
      title: opts.title ?? (opts.destructive ? 'Are you sure?' : 'Please confirm'),
      message: opts.message,
      confirmButtonText: opts.confirmText ?? (opts.destructive ? 'Delete' : 'Confirm'),
      cancelButtonText: opts.cancelText ?? 'Cancel',
      isDestructive: !!opts.destructive
    };
    return this.dialog
      .open(ConfirmationDialogComponent, { width: '420px', maxWidth: '95vw', data, autoFocus: 'dialog' })
      .afterClosed()
      .pipe(map(result => result === true));
  }

  confirmAsync(options: ConfirmOptions | string): Promise<boolean> {
    return firstValueFrom(this.confirm(options));
  }

  /** Informational message with a single OK button (replacement for window.alert). */
  alert(message: string, title = 'Notice'): Observable<boolean> {
    return this.dialog
      .open(ConfirmationDialogComponent, {
        width: '420px',
        maxWidth: '95vw',
        data: { title, message, confirmButtonText: 'OK', hideCancel: true } as ConfirmationDialogData
      })
      .afterClosed()
      .pipe(map(() => true));
  }
}
