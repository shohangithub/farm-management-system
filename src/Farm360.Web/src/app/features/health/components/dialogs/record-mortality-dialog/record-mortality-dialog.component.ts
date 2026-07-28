import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../../services/health.service';
import { CauseOfDeath } from '../../../models/health.models';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-record-mortality-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatIconModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="p-6">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold text-gray-900 dark:text-white flex items-center m-0">
          <div class="w-10 h-10 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center mr-3 border border-red-200 dark:border-red-800">
            <mat-icon class="text-red-600 dark:text-red-400">warning</mat-icon>
          </div>
          Record Mortality
        </h2>
        <button mat-icon-button mat-dialog-close class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div *ngIf="error" class="bg-red-50 border-l-4 border-red-500 p-4 mb-6 rounded-md shadow-sm">
        <div class="flex">
          <mat-icon class="text-red-500 mr-2">error</mat-icon>
          <p class="text-sm text-red-700 font-medium">{{ error }}</p>
        </div>
      </div>

      <form [formGroup]="form" class="space-y-5">
        
        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Select Animal <span class="text-red-500">*</span></label>
          <app-animal-picker formControlName="animalId"></app-animal-picker>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Date of Death <span class="text-red-500">*</span></label>
            <input type="date" formControlName="deathDate"
                   class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 dark:text-white transition-all duration-200">
          </div>

          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Cause of Death <span class="text-red-500">*</span></label>
            <select formControlName="causeOfDeath"
                    class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 dark:text-white transition-all duration-200">
              <option *ngFor="let cause of causes" [value]="cause">{{ cause }}</option>
            </select>
          </div>
        </div>

        <div *ngIf="form.get('causeOfDeath')?.value === 'Disease'">
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Disease Name</label>
          <input type="text" formControlName="diseaseName" placeholder="e.g. Unknown Disease"
                 class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 dark:text-white transition-all duration-200">
        </div>

        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Estimated Economic Loss (BDT)</label>
          <input type="number" formControlName="estimatedEconomicLossBdt" min="0" placeholder="0"
                 class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 dark:text-white transition-all duration-200">
        </div>

        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Post-Mortem Notes</label>
          <textarea formControlName="postMortemNotes" rows="3" placeholder="Any findings or observations..."
                    class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 dark:text-white transition-all duration-200"></textarea>
        </div>

        <div class="flex justify-end gap-3 pt-4 border-t border-gray-100 dark:border-gray-800">
          <button type="button" mat-dialog-close [disabled]="isSubmitting" class="px-5 py-2 text-sm font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-all shadow-sm">
            Cancel
          </button>
          <button type="button" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()"
                  class="px-5 py-2 text-sm font-bold text-white bg-red-600 hover:bg-red-700 rounded-xl transition-all shadow-sm shadow-red-500/30 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
            <mat-icon *ngIf="isSubmitting" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon>
            <span>{{ isSubmitting ? 'Recording...' : 'Record Death' }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class RecordMortalityDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<RecordMortalityDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  causes = Object.values(CauseOfDeath);

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      deathDate: [new Date(), Validators.required],
      causeOfDeath: [CauseOfDeath.Unknown, Validators.required],
      diseaseName: [''],
      postMortemNotes: [''],
      estimatedEconomicLossBdt: [0, Validators.min(0)]
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      this.error = 'No farm context available.';
      this.isSubmitting = false;
      return;
    }

    const request = {
      farmId: farmId,
      animalId: val.animalId,
      deathDate: new Date(val.deathDate).toISOString().split('T')[0],
      causeOfDeath: val.causeOfDeath,
      diseaseName: val.diseaseName,
      postMortemNotes: val.postMortemNotes,
      estimatedEconomicLossBdt: val.estimatedEconomicLossBdt,
      recordedByUserId: '00000000-0000-0000-0000-000000000000' // Mock user ID for MVP
    };

    this.healthService.recordMortality(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to record mortality.';
        this.isSubmitting = false;
      }
    });
  }
}
