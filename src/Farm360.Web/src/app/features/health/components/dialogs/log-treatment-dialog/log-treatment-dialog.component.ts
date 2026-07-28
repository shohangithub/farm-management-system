import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { HealthService } from '../../../services/health.service';
import { AnimalPickerComponent } from '../../../../../shared/components/animal-picker/animal-picker.component';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-log-treatment-dialog',
  standalone: true,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="relative bg-white dark:bg-gray-900 rounded-2xl shadow-2xl overflow-hidden flex flex-col">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/50 flex items-center justify-between">
        <h2 class="text-xl font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg text-purple-600 dark:text-purple-400">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z" />
            </svg>
          </div>
          Log Medical Treatment
        </h2>
      </div>

      <!-- Content -->
      <div class="p-6 overflow-y-auto max-h-[70vh]">
        <form [formGroup]="form" class="flex flex-col gap-5">
          
          <!-- Animal Selection -->
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Select Animal</label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
          </div>

          <!-- Diagnosis -->
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Diagnosis</label>
            <input type="text" formControlName="diagnosis" placeholder="e.g. Mastitis"
              class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white placeholder-gray-400">
          </div>

          <!-- Medication & Dosage -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Medication Name</label>
              <input type="text" formControlName="medicationName"
                class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
            </div>

            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Dosage Amount</label>
              <div class="relative">
                <input type="number" formControlName="dosageAmount"
                  class="w-full pl-4 pr-16 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
                <div class="absolute inset-y-0 right-0 flex items-center pr-4">
                  <span class="text-sm font-medium text-gray-500">{{ form.get('dosageUnit')?.value || 'unit' }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Withdrawals -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Milk Withdrawal (Days)</label>
              <input type="number" formControlName="milkWithdrawalDays"
                class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
            </div>

            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Meat Withdrawal (Days)</label>
              <input type="number" formControlName="meatWithdrawalDays"
                class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
            </div>
          </div>
          
          <!-- Date & Cost -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Start Date</label>
              <input type="date" formControlName="startDate"
                class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
            </div>
            
            <div>
              <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Cost (BDT)</label>
              <input type="number" formControlName="costBdt"
                class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
            </div>
          </div>

          <!-- Veterinarian Name -->
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Veterinarian Name (Optional)</label>
            <input type="text" formControlName="veterinarianName"
              class="w-full px-4 py-2.5 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white">
          </div>

          <!-- Notes -->
          <div>
            <label class="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1.5">Notes</label>
            <textarea formControlName="notes" rows="3"
              class="w-full px-4 py-3 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-purple-500/20 focus:border-purple-500 transition-colors text-gray-900 dark:text-white resize-none"></textarea>
          </div>
          
          <div *ngIf="error" class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl">
            <p class="text-sm text-red-600 dark:text-red-400 font-medium flex items-center gap-2">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
              </svg>
              {{ error }}
            </p>
          </div>
        </form>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/50 flex justify-end gap-3">
        <button mat-dialog-close [disabled]="isSubmitting"
          class="px-5 py-2.5 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors shadow-sm focus:ring-2 focus:ring-gray-200 disabled:opacity-50">
          Cancel
        </button>
        <button (click)="onSubmit()" [disabled]="form.invalid || isSubmitting"
          class="px-5 py-2.5 text-sm font-semibold text-white bg-purple-600 hover:bg-purple-700 border border-transparent rounded-xl transition-colors shadow-sm focus:ring-2 focus:ring-purple-500/50 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
          <svg *ngIf="isSubmitting" class="animate-spin h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          {{ isSubmitting ? 'Saving...' : 'Save Treatment' }}
        </button>
      </div>
    </div>
  `
})
export class LogTreatmentDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<LogTreatmentDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';

  constructor() {
    this.form = this.fb.group({
      animalId: ['', Validators.required],
      diagnosis: ['', Validators.required],
      medicationName: ['', Validators.required],
      dosageAmount: [1, [Validators.required, Validators.min(0.1)]],
      dosageUnit: ['ml', Validators.required],
      milkWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      meatWithdrawalDays: [0, [Validators.required, Validators.min(0)]],
      startDate: [new Date(), Validators.required],
      costBdt: [0, [Validators.required, Validators.min(0)]],
      veterinarianName: [''],
      notes: ['']
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
      diagnosis: val.diagnosis,
      medicationName: val.medicationName,
      dosageAmount: val.dosageAmount,
      dosageUnit: val.dosageUnit,
      milkWithdrawalDays: val.milkWithdrawalDays,
      meatWithdrawalDays: val.meatWithdrawalDays,
      startDate: new Date(val.startDate).toISOString().split('T')[0],
      costBdt: val.costBdt,
      veterinarianName: val.veterinarianName,
      notes: val.notes
    };

    this.healthService.logTreatment(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.error = err.error?.detail || 'Failed to log treatment. Please try again.';
        this.isSubmitting = false;
      }
    });
  }
}
