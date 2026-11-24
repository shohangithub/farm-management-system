import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/**
 * Shown when a page's primary data load fails, so a failure never looks like "no data".
 * Pair with `parseApiError(err)` for the message.
 */
@Component({
  selector: 'app-error-state',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="alert"
         class="flex flex-col items-center justify-center text-center bg-white dark:bg-gray-800 rounded-xl border border-dashed border-rose-200 dark:border-rose-900/50"
         [class.p-12]="!compact" [class.p-4]="compact">
      <div class="rounded-full flex items-center justify-center bg-rose-50 dark:bg-rose-900/20"
           [class.h-16]="!compact" [class.w-16]="!compact" [class.mb-4]="!compact"
           [class.h-10]="compact" [class.w-10]="compact" [class.mb-2]="compact">
        <mat-icon class="text-rose-500 dark:text-rose-400" aria-hidden="true">{{ icon }}</mat-icon>
      </div>
      <h3 class="font-medium text-gray-900 dark:text-white" [class.text-lg]="!compact" [class.text-sm]="compact">{{ title }}</h3>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400 max-w-md whitespace-pre-line">{{ message }}</p>
      <div [class.mt-6]="!compact" [class.mt-3]="compact" *ngIf="showRetry">
        <button mat-stroked-button color="primary" type="button" (click)="retry.emit()">
          <mat-icon class="mr-2" aria-hidden="true">refresh</mat-icon>
          {{ retryLabel }}
        </button>
      </div>
    </div>
  `
})
export class ErrorStateComponent {
  @Input() title = "Couldn't load data";
  @Input() message = 'Something went wrong while loading. Please check your connection and try again.';
  @Input() icon = 'cloud_off';
  @Input() retryLabel = 'Retry';
  @Input() showRetry = true;
  /** Smaller padding for widgets / cards. */
  @Input() compact = false;

  @Output() retry = new EventEmitter<void>();
}
