import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { CauseOfDeath } from '../../../models/health.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../../shared/utils/local-date';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-record-mortality-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">sentiment_very_dissatisfied</mat-icon>
            Record Mortality
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Log and track animal mortality</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors" aria-label="Close">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Select Animal <span class="text-red-500">*</span></label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
            <p *ngIf="form.get('animalId')?.touched && form.get('animalId')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Date of Death <span class="text-red-500">*</span></label>
              <input type="date" formControlName="deathDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              <p *ngIf="form.get('deathDate')?.touched && form.get('deathDate')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Cause of Death <span class="text-red-500">*</span></label>
              <select formControlName="causeOfDeath"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option *ngFor="let cause of causes" [ngValue]="cause.value">{{ cause.label }}</option>
              </select>
            </div>
          </div>

          <div *ngIf="form.get('causeOfDeath')?.value === CauseOfDeath.Disease" class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Disease Name <span class="text-red-500">*</span></label>
            <input type="text" formControlName="diseaseName" placeholder="e.g. Foot and Mouth Disease"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p *ngIf="form.get('diseaseName')?.touched && form.get('diseaseName')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">Disease name is required when the cause is Disease.</p>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Estimated Economic Loss (BDT)</label>
            <input type="number" formControlName="estimatedEconomicLossBdt" min="0" placeholder="0"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p *ngIf="form.get('estimatedEconomicLossBdt')?.touched && form.get('estimatedEconomicLossBdt')?.hasError('min')" class="text-xs text-red-600 dark:text-red-400 mt-1">Must be at least 0.</p>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Post-Mortem Notes</label>
            <textarea formControlName="postMortemNotes" rows="3" placeholder="Any findings or observations..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isSubmitting() ? 'Recording...' : 'Record Death' }}</span>
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar {
      width: 6px;
    }
    .custom-scrollbar::-webkit-scrollbar-track {
      background: transparent;
    }
    .custom-scrollbar::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.5);
      border-radius: 20px;
    }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.8);
    }
  `]
})
export class RecordMortalityDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<RecordMortalityDialog>);
  /** Opened from an animal's page: that animal is preselected. */
  private data = inject<{ animalId?: string } | null>(MAT_DIALOG_DATA, { optional: true });

  form: FormGroup;
  readonly CauseOfDeath = CauseOfDeath;
  isSubmitting = signal(false);
  error = signal('');
  causes = [
    { value: CauseOfDeath.Disease, label: 'Disease' },
    { value: CauseOfDeath.Accident, label: 'Accident' },
    { value: CauseOfDeath.NaturalCauses, label: 'Natural Causes' },
    { value: CauseOfDeath.Unknown, label: 'Unknown' },
    { value: CauseOfDeath.Slaughter, label: 'Slaughter' }
  ];

  constructor() {
    this.form = this.fb.group({
      animalId: [this.data?.animalId ?? '', Validators.required],
      // <input type="date"> needs a local 'yyyy-MM-dd' string; a Date object shows blank.
      deathDate: [toLocalIsoDate(), Validators.required],
      causeOfDeath: [CauseOfDeath.Unknown, Validators.required],
      diseaseName: [''],
      postMortemNotes: [''],
      estimatedEconomicLossBdt: [0, Validators.min(0)]
    });

    // The server requires a disease name when the cause is Disease.
    const diseaseName = this.form.get('diseaseName')!;
    this.form.get('causeOfDeath')!.valueChanges.pipe(takeUntilDestroyed()).subscribe(cause => {
      if (cause === CauseOfDeath.Disease) {
        diseaseName.setValidators([Validators.required, Validators.pattern(/\S/)]);
      } else {
        diseaseName.clearValidators();
      }
      diseaseName.updateValueAndValidity();
    });
  }

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      const msg = 'No farm context available.';
      this.error.set(msg);
      this.isSubmitting.set(false);
      return;
    }

    const request = {
      farmId: farmId,
      animalId: val.animalId,
      // Already a local 'yyyy-MM-dd' string from the date input.
      deathDate: val.deathDate,
      // Sent by name; the API reads enums as strings (Number() of 'Disease' gave NaN -> null).
      causeOfDeath: val.causeOfDeath,
      diseaseName: val.causeOfDeath === CauseOfDeath.Disease ? (val.diseaseName ?? '').trim() : null,
      postMortemNotes: val.postMortemNotes,
      estimatedEconomicLossBdt: val.estimatedEconomicLossBdt,
      recordedByUserId: '00000000-0000-0000-0000-000000000000'
    };

    this.healthService.recordMortality(request).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        const parsedMsg = parseApiError(err, 'Failed to record mortality.');
        this.error.set(parsedMsg);
        this.isSubmitting.set(false);
      }
    });
  }
}
