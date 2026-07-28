import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../../services/health.service';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';

@Component({
  selector: 'app-schedule-vaccination-dialog',
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
          <div class="w-10 h-10 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center mr-3 border border-blue-200 dark:border-blue-800">
            <mat-icon class="text-blue-600 dark:text-blue-400">vaccines</mat-icon>
          </div>
          Schedule Vaccination
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

        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Vaccine Name <span class="text-red-500">*</span></label>
          <input type="text" formControlName="vaccineName" placeholder="e.g. FMD Vaccine"
                 class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:text-white transition-all duration-200">
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Batch Number <span class="text-red-500">*</span></label>
            <input type="text" formControlName="batchNumber"
                   class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:text-white transition-all duration-200">
          </div>

          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Scheduled Date <span class="text-red-500">*</span></label>
            <input type="date" formControlName="scheduledDate"
                   class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:text-white transition-all duration-200">
          </div>
        </div>

        <div>
          <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Notes (Optional)</label>
          <textarea formControlName="notes" rows="2" placeholder="Any additional information..."
                    class="w-full px-4 py-2 bg-gray-50 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 dark:text-white transition-all duration-200"></textarea>
        </div>

        <div class="flex justify-end gap-3 pt-4 border-t border-gray-100 dark:border-gray-800">
          <button type="button" mat-dialog-close [disabled]="isSubmitting" class="px-5 py-2 text-sm font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-all shadow-sm">
            Cancel
          </button>
          <button type="button" [disabled]="form.invalid || isSubmitting" (click)="onSubmit()"
                  class="px-5 py-2 text-sm font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl transition-all shadow-sm shadow-blue-500/30 flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
            <mat-icon *ngIf="isSubmitting" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon>
            <span>{{ isSubmitting ? 'Scheduling...' : 'Schedule Vaccine' }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class ScheduleVaccinationDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private dialogRef = inject(MatDialogRef<ScheduleVaccinationDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      vaccineName: ['', Validators.required],
      batchNumber: ['', Validators.required],
      scheduledDate: [new Date(), Validators.required],
      notes: ['']
    });
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';

    const val = this.form.value;
    const formattedDate = new Date(val.scheduledDate).toISOString().split('T')[0];

    this.healthService.scheduleVaccination(
      val.animalId, 
      val.vaccineName, 
      val.batchNumber, 
      formattedDate, 
      val.notes
    ).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to schedule vaccination.';
        this.isSubmitting = false;
      }
    });
  }
}
