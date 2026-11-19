import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/** Friendly page shown when the permission guard blocks a screen (instead of a blank page). */
@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [RouterLink, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center text-center py-24 px-4">
      <mat-icon class="!w-12 !h-12 !text-5xl text-amber-500 mb-4">lock</mat-icon>
      <h1 class="text-xl font-semibold text-gray-900 dark:text-gray-100 mb-2">You don't have access to this page</h1>
      <p class="text-sm text-gray-500 dark:text-gray-400 max-w-md mb-6">
        Your role does not include the permission needed for this screen. Ask your organisation's
        owner or administrator to grant it if you need access.
      </p>
      <a mat-flat-button color="primary" routerLink="/dashboard">Back to dashboard</a>
    </div>
  `,
})
export class AccessDeniedComponent {}
