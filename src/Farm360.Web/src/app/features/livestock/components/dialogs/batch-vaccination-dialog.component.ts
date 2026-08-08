import { Component, Inject, signal, computed, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AnimalListItemDto } from '../../models/animal.models';
import { HealthService } from '../../../health/services/health.service';
import { HttpErrorResponse } from '@angular/common/http';
import { parseApiError } from '../../../../core/utils/error-parser';

export interface BatchVaccinationDialogData {
  animals: AnimalListItemDto[];
}

@Component({
  selector: 'app-batch-vaccination-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, 
    ReactiveFormsModule, 
    MatDialogModule, 
    MatIconModule
  ],
  template: `
    <div class="flex flex-col h-full bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl border border-gray-100 dark:border-gray-800">
      
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex justify-between items-center bg-gray-50/50 dark:bg-gray-800/50">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shadow-sm">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">vaccines</mat-icon>
          </div>
          <div>
            <h2 class="text-lg font-bold text-gray-900 dark:text-white leading-tight">Batch Vaccination</h2>
            <p class="text-xs text-gray-500 font-medium">Administer vaccine to {{ data.animals.length }} animals</p>
          </div>
        </div>
        <button type="button" mat-dialog-close class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-gray-200 dark:hover:bg-gray-700 text-gray-500 transition-colors">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">close</mat-icon>
        </button>
      </div>

      <!-- Error Alert -->
      <div *ngIf="error()" class="m-6 mb-0 p-4 bg-red-50 dark:bg-red-900/20 border-l-4 border-red-500 rounded-r-lg flex items-start gap-3 animate-fade-in-up">
        <mat-icon class="text-red-500 mt-0.5 !text-[20px] !w-[20px] !h-[20px]">error_outline</mat-icon>
        <div>
          <h4 class="text-sm font-bold text-red-800 dark:text-red-300">{{ error()?.title || 'Submission Failed' }}</h4>
          <p class="text-xs text-red-700 dark:text-red-400 mt-1">{{ error()?.detail || 'An unexpected error occurred. Please try again.' }}</p>
          <ul *ngIf="error()?.errors" class="mt-2 text-xs text-red-700 dark:text-red-400 list-disc list-inside">
            <li *ngFor="let err of error()?.errors | keyvalue">{{ err.value }}</li>
          </ul>
        </div>
      </div>

      <!-- Content -->
      <div class="p-6 flex-1 overflow-y-auto">
        <form [formGroup]="form" class="space-y-5">
          <!-- Vaccine Name -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Vaccine Name <span class="text-red-500">*</span></label>
            <input type="text" formControlName="vaccineName" placeholder="e.g. FMD Vaccine"
                   class="w-full px-4 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 shadow-sm transition-colors"
                   [class.border-red-300]="form.get('vaccineName')?.invalid && form.get('vaccineName')?.touched">
            <p *ngIf="form.get('vaccineName')?.invalid && form.get('vaccineName')?.touched" class="mt-1 text-xs text-red-500">
              Vaccine name is required.
            </p>
          </div>

          <!-- Batch Number & Date -->
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Batch Number</label>
              <input type="text" formControlName="batchNumber" placeholder="e.g. BN-1002"
                     class="w-full px-4 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 shadow-sm transition-colors">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Administered Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="administeredDate"
                     class="w-full px-4 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 shadow-sm transition-colors"
                     [class.border-red-300]="form.get('administeredDate')?.invalid && form.get('administeredDate')?.touched">
            </div>
          </div>

          <!-- Notes -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Notes</label>
            <textarea formControlName="notes" rows="2" placeholder="Optional notes about this batch vaccination..."
                      class="w-full px-4 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 shadow-sm transition-colors resize-none"></textarea>
          </div>
        </form>

        <!-- Selected Animals List -->
        <div class="mt-8">
          <h3 class="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3 flex items-center gap-2">
            Selected Animals <span class="bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 py-0.5 px-2 rounded-full text-[10px]">{{ data.animals.length }}</span>
          </h3>
          <div class="border border-gray-100 dark:border-gray-800 rounded-xl overflow-hidden bg-gray-50/50 dark:bg-gray-900/50 max-h-48 overflow-y-auto">
            <table class="w-full text-left text-sm whitespace-nowrap">
              <thead class="bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400 text-xs uppercase font-bold sticky top-0 z-10">
                <tr>
                  <th class="px-4 py-2 border-b border-gray-200 dark:border-gray-700">Tag ID</th>
                  <th class="px-4 py-2 border-b border-gray-200 dark:border-gray-700">Breed</th>
                  <th class="px-4 py-2 border-b border-gray-200 dark:border-gray-700 text-right">Age</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr *ngFor="let a of data.animals" class="hover:bg-white dark:hover:bg-gray-800 transition-colors">
                  <td class="px-4 py-2 font-semibold text-gray-900 dark:text-white">{{ a.tagId }}</td>
                  <td class="px-4 py-2 text-gray-500">{{ a.breedName }}</td>
                  <td class="px-4 py-2 text-gray-500 text-right">{{ ageLabel(a.dateOfBirth) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Footer Actions -->
      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/50 flex justify-end gap-3">
        <button type="button" mat-dialog-close [disabled]="isSubmitting()"
                class="px-5 py-2.5 text-sm font-semibold text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 shadow-sm transition-colors disabled:opacity-50">
          Cancel
        </button>
        <button type="button" (click)="onSubmit()" [disabled]="form.invalid || isSubmitting()"
                class="px-5 py-2.5 text-sm font-bold text-white bg-emerald-600 border border-transparent rounded-lg hover:bg-emerald-700 shadow-sm transition-all focus:ring-2 focus:ring-offset-2 focus:ring-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
          <mat-icon *ngIf="isSubmitting()" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">autorenew</mat-icon>
          {{ isSubmitting() ? 'Saving...' : 'Confirm Vaccination' }}
        </button>
      </div>

    </div>
  `
})
export class BatchVaccinationDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly healthSvc = inject(HealthService);

  readonly error = signal<any>(null);
  readonly isSubmitting = signal(false);

  readonly form = this.fb.group({
    vaccineName: ['', Validators.required],
    batchNumber: [''],
    administeredDate: [new Date().toISOString().substring(0, 10), Validators.required],
    notes: ['']
  });

  constructor(
    public dialogRef: MatDialogRef<BatchVaccinationDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BatchVaccinationDialogData
  ) {}

  ngOnInit(): void {}

  ageLabel(dob: string): string {
    const days = Math.floor((Date.now() - new Date(dob).getTime()) / 86_400_000);
    if (days < 30)  return `${days}d`;
    if (days < 365) return `${Math.floor(days / 30)}mo`;
    const y = Math.floor(days / 365);
    const m = Math.floor((days % 365) / 30);
    return m > 0 ? `${y}y ${m}m` : `${y}y`;
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.error.set(null);

    const val = this.form.value;
    const animalIds = this.data.animals.map(a => a.id);
    
    // Sanitize payload empty strings to null
    const payload = {
      animalIds,
      vaccineName: val.vaccineName!,
      batchNumber: val.batchNumber?.trim() || null,
      administeredDate: val.administeredDate!,
      notes: val.notes?.trim() || null
    };

    this.healthSvc.batchAdministerVaccination(
      payload.animalIds,
      payload.vaccineName,
      payload.batchNumber!,
      payload.administeredDate,
      payload.notes!
    ).subscribe({
      next: () => {
        this.dialogRef.close(true);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(parseApiError(err));
        this.isSubmitting.set(false);
      }
    });
  }
}
