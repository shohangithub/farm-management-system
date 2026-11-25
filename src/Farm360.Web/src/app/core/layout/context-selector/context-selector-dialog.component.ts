import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { ContextSelectorComponent } from './context-selector.component';
import { TranslatePipe } from '../../i18n/translate.pipe';

/** Mobile-friendly wrapper: shows the organisation / branch / farm selectors stacked full-width. */
@Component({
  selector: 'app-context-selector-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, ContextSelectorComponent, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'Working context' | t }}</h2>
    <mat-dialog-content>
      <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">{{ 'Choose the organisation, branch and farm you are working in.' | t }}</p>
      <app-context-selector [stacked]="true"></app-context-selector>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button color="primary" mat-dialog-close>{{ 'Done' | t }}</button>
    </mat-dialog-actions>
  `
})
export class ContextSelectorDialogComponent {}
