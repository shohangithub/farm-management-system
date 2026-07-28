import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
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
    MatIconModule,
    AnimalPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">medical_services</mat-icon>
            Log Medical Treatment
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Record animal medical treatment and medication</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error" class="mx-6 mt-4 p-3 bg-red-50 text-red-700 border border-red-200 rounded-lg text-sm whitespace-pre-wrap">
          {{ error }}
        </div>

        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          
          <!-- Animal Selection -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Select Animal <span class="text-red-500">*</span></label>
            <app-animal-picker formControlName="animalId"></app-animal-picker>
          </div>

          <!-- Diagnosis -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Diagnosis <span class="text-red-500">*</span></label>
            <input type="text" formControlName="diagnosis" placeholder="e.g. Mastitis"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <!-- Medication & Dosage -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Medication Name <span class="text-red-500">*</span></label>
              <input type="text" formControlName="medicationName" placeholder="e.g. Penicillin"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Dosage Amount <span class="text-red-500">*</span></label>
              <div class="relative">
                <input type="number" formControlName="dosageAmount" placeholder="1"
                       class="block w-full px-3 pr-16 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <div class="absolute inset-y-0 right-0 flex items-center pr-3">
                  <span class="text-sm font-medium text-gray-500">{{ form.get('dosageUnit')?.value || 'unit' }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Withdrawals -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Milk Withdrawal (Days) <span class="text-red-500">*</span></label>
              <input type="number" formControlName="milkWithdrawalDays" placeholder="0" min="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Meat Withdrawal (Days) <span class="text-red-500">*</span></label>
              <input type="number" formControlName="meatWithdrawalDays" placeholder="0" min="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>
          
          <!-- Date & Cost -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Start Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="startDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
            
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Cost (BDT)</label>
              <input type="number" formControlName="costBdt" placeholder="0" min="0"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <!-- Veterinarian Name -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Veterinarian Name (Optional)</label>
            <input type="text" formControlName="veterinarianName" placeholder="Dr. Name"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <!-- Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes</label>
            <textarea formControlName="notes" rows="3" placeholder="Optional details..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>
        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isSubmitting"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed">
            <span *ngIf="!isSubmitting">Save Treatment</span>
            <span *ngIf="isSubmitting">Saving...</span>
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
