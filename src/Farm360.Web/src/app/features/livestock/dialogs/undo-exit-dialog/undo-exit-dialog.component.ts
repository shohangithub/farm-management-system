import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AnimalService } from '../../services/animal.service';
import { ReverseAnimalExitResult } from '../../models/animal.models';
import { parseApiError } from '../../../../core/utils/error-parser';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface UndoExitDialogData {
  animalId: string;
  animalTag: string;
  /** 'sold' or 'died' */
  kind: 'sold' | 'died';
  exitDate?: string | null;
}

/**
 * Undo a sale or death recorded by mistake. Explains what will be put back, asks for a reason,
 * and closes with the server's result.
 */
@Component({
  selector: 'app-undo-exit-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, MatDialogModule, MatIconModule, TranslatePipe],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-amber-50/60 dark:bg-amber-900/10 flex items-center justify-between">
        <div>
          <h2 class="m-0 text-lg font-bold text-amber-800 dark:text-amber-300 flex items-center gap-2">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">undo</mat-icon>
            {{ (data.kind === 'sold' ? 'Undo sale' : 'Undo death') | t }}
          </h2>
          <p class="m-0 text-xs text-amber-700/80 dark:text-amber-400/80 mt-0.5">
            {{ (data.kind === 'sold' ? '{tag} was recorded as sold.' : '{tag} was recorded as dead.') | t: { tag: data.animalTag } }}<span *ngIf="data.exitDate"> ({{ data.exitDate | date:'d MMM yyyy' }})</span>
            {{ 'Use this only if that was a mistake.' | t }}
          </p>
        </div>
        <button [attr.aria-label]="'Close' | t" mat-dialog-close [disabled]="submitting()"
          class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <div class="p-6 space-y-4 overflow-y-auto text-sm text-gray-700 dark:text-gray-300">
        <div class="rounded-xl border border-gray-200 dark:border-gray-700 p-4">
          <p class="m-0 mb-2 font-semibold text-gray-900 dark:text-white">{{ 'What will be put back' | t }}</p>
          <ul class="m-0 pl-5 space-y-1 list-disc text-[13px]">
            <li>{{ '{tag} is back on the farm (Active), in the pen it left.' | t: { tag: data.animalTag } }}</li>
            <li *ngIf="data.kind === 'sold'">{{ saleUndoText }}</li>
            <li *ngIf="data.kind === 'died'">{{ 'The mortality record and the death loss in the accounts are removed.' | t }}</li>
            <li>{{ 'Vaccinations and treatments that were closed because of it are open again.' | t }}</li>
            <li>{{ 'Its feeding plans run again; feed entries skipped because of it are pending again, to confirm on Confirm Planned Feed.' | t }}</li>
            <li>{{ 'Labour and overhead are shared with it again.' | t }}</li>
          </ul>
        </div>

        <label class="block">
          <span class="block text-xs font-semibold text-gray-600 dark:text-gray-400 mb-1">{{ 'Why is it being undone?' | t }} <span class="text-red-500">*</span></span>
          <textarea [(ngModel)]="reason" rows="2" maxlength="300" [placeholder]="'e.g. Wrong animal selected' | t"
            class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-amber-500 focus:border-amber-500"></textarea>
        </label>

        <div *ngIf="error()" class="p-3 bg-red-50 text-red-700 border border-red-200 rounded-lg text-sm whitespace-pre-wrap">{{ error() }}</div>
      </div>

      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3">
        <button type="button" mat-dialog-close [disabled]="submitting()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
          {{ 'Cancel' | t }}
        </button>
        <button type="button" (click)="submit()" [disabled]="submitting() || !reason.trim()"
          class="px-4 py-2 text-sm font-semibold text-white bg-amber-600 rounded-xl hover:bg-amber-700 transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed">
          {{ (submitting() ? 'Undoing…' : (data.kind === 'sold' ? 'Undo sale' : 'Undo death')) | t }}
        </button>
      </div>
    </div>
  `
})
export class UndoExitDialogComponent {
  readonly data = inject<UndoExitDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<UndoExitDialogComponent, ReverseAnimalExitResult | undefined>);
  private readonly animals = inject(AnimalService);
  private readonly i18n = inject(I18nService);
  readonly saleUndoText = this.i18n.t("The sale income and cost of the animal sold are removed from the accounts. A credit sale's bill is cancelled; money already received from the buyer is removed too, so hand it back.");

  reason = '';
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    if (!this.reason.trim()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.animals.reverseExit(this.data.animalId, this.reason.trim()).subscribe({
      next: result => this.dialogRef.close(result),
      error: err => {
        this.submitting.set(false);
        this.error.set(parseApiError(err, this.i18n.t('Could not undo it.')));
      }
    });
  }
}
