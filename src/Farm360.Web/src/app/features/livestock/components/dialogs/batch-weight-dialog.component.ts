import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin, finalize } from 'rxjs';
import { AnimalService } from '../../services/animal.service';
import { AnimalListItemDto, RecordWeightRequest } from '../../models/animal.models';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { NotificationService } from '../../../../shared/services/notification.service';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface BatchWeightDialogData {
  animals: AnimalListItemDto[];
}

interface AnimalWeightInput {
  animal: AnimalListItemDto;
  weightKg: number | null;
  notes: string;
}

@Component({
  selector: 'app-batch-weight-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 relative bg-white dark:bg-surface-dark rounded-2xl shadow-xl overflow-hidden">
      <!-- Header -->
      <div class="flex items-start justify-between mb-6">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-full bg-primary-50 dark:bg-primary-900/20 flex items-center justify-center text-primary-600 dark:text-primary-400 shrink-0 shadow-sm border border-primary-100 dark:border-primary-800">
            <mat-icon>scale</mat-icon>
          </div>
          <div>
            <h2 class="text-xl font-bold text-gray-900 dark:text-white m-0">{{ 'Batch Weight Record' | t }}</h2>
            <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ 'Record new weights for {n} selected animals' | t: { n: inputs().length } }}</p>
          </div>
        </div>
        <button [attr.aria-label]="'Close' | t" mat-icon-button (click)="close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content Table -->
      <div class="max-h-[60vh] overflow-y-auto pr-2 -mr-2">
        <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700/50">
          <thead class="bg-gray-50 dark:bg-gray-800/50 sticky top-0 z-10">
            <tr>
              <th scope="col" class="px-4 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider rounded-tl-lg">{{ 'Animal Tag' | t }}</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">{{ 'Weight (kg)' | t }}</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider rounded-tr-lg">{{ 'Notes' | t }}</th>
            </tr>
          </thead>
          <tbody class="bg-white dark:bg-surface-dark divide-y divide-gray-100 dark:divide-gray-800">
            <tr *ngFor="let item of inputs(); let i = index">
              <td class="px-4 py-3 whitespace-nowrap">
                <div class="flex flex-col">
                  <span class="text-sm font-bold text-gray-900 dark:text-white">{{ item.animal.tagId }}</span>
                  <span class="text-xs text-gray-500">{{ item.animal.breedName }}</span>
                </div>
              </td>
              <td class="px-4 py-3 whitespace-nowrap">
                <input type="number" min="0" step="0.1" [(ngModel)]="inputs()[i].weightKg" [attr.aria-label]="'Weight (kg) for {tag}' | t: { tag: item.animal.tagId }"
                       class="block w-24 pl-3 pr-3 py-1.5 text-sm border border-gray-200 dark:border-gray-700 rounded-md bg-gray-50 dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 shadow-inner"
                       [placeholder]="'e.g. 50' | t">
              </td>
              <td class="px-4 py-3">
                <input type="text" [(ngModel)]="inputs()[i].notes"
                       class="block w-full pl-3 pr-3 py-1.5 text-sm border border-gray-200 dark:border-gray-700 rounded-md bg-gray-50 dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500 shadow-inner"
                       [placeholder]="'Optional notes' | t">
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      
      <!-- Date Picker for all -->
      <div class="mt-6 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-xl border border-gray-100 dark:border-gray-700">
        <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">{{ 'Recording Date (For all)' | t }}</label>
        <input type="date" [(ngModel)]="recordedDate"
               class="block w-full sm:w-64 pl-3 pr-3 py-2 border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-900 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary-500 shadow-sm">
      </div>

      <!-- Footer Actions -->
      <div class="mt-6 flex justify-end gap-3 pt-4 border-t border-gray-100 dark:border-gray-800">
        <button mat-button (click)="close()" [disabled]="isSubmitting()">{{ 'Cancel' | t }}</button>
        <button mat-flat-button color="primary" class="!px-6 !py-1"
                [disabled]="isSubmitting() || !isValid()"
                (click)="submit()">
          <span class="flex items-center gap-2">
            <mat-spinner *ngIf="isSubmitting()" diameter="18" class="text-white"></mat-spinner>
            <span>{{ (isSubmitting() ? 'Saving...' : 'Save Weights') | t }}</span>
          </span>
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }
  `]
})
export class BatchWeightDialogComponent {
  private readonly notify = inject(NotificationService);
  private dialogRef = inject(MatDialogRef<BatchWeightDialogComponent>);
  private data = inject<BatchWeightDialogData>(MAT_DIALOG_DATA);
  private svc = inject(AnimalService);
  private readonly i18n = inject(I18nService);

  readonly isSubmitting = signal(false);
  
  // State
  readonly inputs = signal<AnimalWeightInput[]>(
    this.data.animals.map(a => ({
      animal: a,
      weightKg: null,
      notes: ''
    }))
  );
  
  recordedDate: string = toLocalIsoDate();

  isValid(): boolean {
    return this.inputs().every(i => i.weightKg != null && i.weightKg > 0);
  }

  close(): void {
    if (!this.isSubmitting()) {
      this.dialogRef.close();
    }
  }

  submit(): void {
    if (!this.isValid()) return;
    this.isSubmitting.set(true);

    const requests = this.inputs().map(input => {
      const payload: RecordWeightRequest = {
        weightKg: input.weightKg!,
        recordedDate: this.recordedDate,
        notes: input.notes
      };
      return this.svc.recordWeight(input.animal.id, payload);
    });

    forkJoin(requests)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (err) => {
          console.error(err);
          this.notify.error(this.i18n.t('Failed to save some weights. Please try again.'));
        }
      });
  }
}
