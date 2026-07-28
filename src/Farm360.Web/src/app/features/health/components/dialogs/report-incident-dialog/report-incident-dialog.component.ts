import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { HealthService } from '../../../services/health.service';
import { IncidentSeverity } from '../../../models/health.models';
import { WorkingContextService } from '../../../../../core/services/working-context.service';

@Component({
  selector: 'app-report-incident-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule
  ],
  template: `
    <div class="bg-white dark:bg-surface-dark rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-gray-500">report_problem</mat-icon>
            Report Disease Incident
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Record a new disease outbreak or incident</p>
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
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Disease Name <span class="text-red-500">*</span></label>
            <input type="text" formControlName="diseaseName" placeholder="e.g. FMD Outbreak"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Severity <span class="text-red-500">*</span></label>
              <select formControlName="severity"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option *ngFor="let sev of severities" [ngValue]="sev.value">{{ sev.label }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Incident Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="incidentDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Affected Animal Count <span class="text-red-500">*</span></label>
            <input type="number" formControlName="affectedAnimalCount" min="1" placeholder="1"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Symptoms <span class="text-red-500">*</span></label>
            <textarea formControlName="symptoms" rows="3" placeholder="Describe the symptoms observed..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
          </div>
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes (Optional)</label>
            <textarea formControlName="notes" rows="2" placeholder="Any additional information..."
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
            <span *ngIf="!isSubmitting">Report Incident</span>
            <span *ngIf="isSubmitting">Reporting...</span>
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
export class ReportIncidentDialog {
  private fb = inject(FormBuilder);
  private healthService = inject(HealthService);
  private contextService = inject(WorkingContextService);
  private dialogRef = inject(MatDialogRef<ReportIncidentDialog>);

  form: FormGroup;
  isSubmitting = false;
  error = '';
  severities = [
    { value: IncidentSeverity.Mild, label: 'Mild' },
    { value: IncidentSeverity.Moderate, label: 'Moderate' },
    { value: IncidentSeverity.Severe, label: 'Severe' },
    { value: IncidentSeverity.Critical, label: 'Critical' }
  ];

  constructor() {
    this.form = this.fb.group({
      diseaseName: ['', [Validators.required, Validators.maxLength(150)]],
      severity: [IncidentSeverity.Moderate, Validators.required],
      incidentDate: [new Date().toISOString().split('T')[0], Validators.required],
      symptoms: ['', [Validators.required, Validators.maxLength(1000)]],
      affectedAnimalCount: [1, [Validators.required, Validators.min(1)]],
      notes: ['', Validators.maxLength(1000)]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting = true;
    this.error = '';
    
    const formValue = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      this.error = 'No farm context available.';
      this.isSubmitting = false;
      return;
    }

    const request = {
      farmId: farmId,
      ...formValue,
      severity: Number(formValue.severity)
    };

    this.healthService.reportIncident(request).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.dialogRef.close(true);
      },
      error: (err) => {
        console.error(err);
        this.error = err.error?.detail || err.error?.title || 'Failed to report incident. Please try again.';
        this.isSubmitting = false;
      }
    });
  }
}
