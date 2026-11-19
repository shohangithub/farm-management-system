import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { HealthService } from '../../../services/health.service';
import { IncidentSeverity } from '../../../models/health.models';
import { WorkingContextService } from '../../../../../core/services/working-context.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../../shared/utils/local-date';
import { NotificationService } from '../../../../../shared/services/notification.service';

@Component({
  selector: 'app-report-incident-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    MatSnackBarModule
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
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Disease Name <span class="text-red-500">*</span></label>
            <input type="text" formControlName="diseaseName" placeholder="e.g. FMD Outbreak"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p *ngIf="form.get('diseaseName')?.touched && form.get('diseaseName')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            <p *ngIf="form.get('diseaseName')?.touched && form.get('diseaseName')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">Must be 150 characters or fewer.</p>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Severity <span class="text-red-500">*</span></label>
              <select formControlName="severity"
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
                <option *ngFor="let sev of severities" [ngValue]="sev.value">{{ sev.label }}</option>
              </select>
              <p *ngIf="form.get('severity')?.touched && form.get('severity')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Incident Date <span class="text-red-500">*</span></label>
              <input type="date" formControlName="incidentDate"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              <p *ngIf="form.get('incidentDate')?.touched && form.get('incidentDate')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Affected Animal Count <span class="text-red-500">*</span></label>
            <input type="number" formControlName="affectedAnimalCount" min="1" placeholder="1"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
            <p *ngIf="form.get('affectedAnimalCount')?.touched && form.get('affectedAnimalCount')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            <p *ngIf="form.get('affectedAnimalCount')?.touched && form.get('affectedAnimalCount')?.hasError('min')" class="text-xs text-red-600 dark:text-red-400 mt-1">Must be at least 1.</p>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Symptoms <span class="text-red-500">*</span></label>
            <textarea formControlName="symptoms" rows="3" placeholder="Describe symptoms..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
            <p *ngIf="form.get('symptoms')?.touched && form.get('symptoms')?.hasError('required')" class="text-xs text-red-600 dark:text-red-400 mt-1">This field is required.</p>
            <p *ngIf="form.get('symptoms')?.touched && form.get('symptoms')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">Must be 1000 characters or fewer.</p>
          </div>
          
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes / Action Plan</label>
            <textarea formControlName="notes" rows="2" placeholder="Quarantine details, notes..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow resize-none"></textarea>
            <p *ngIf="form.get('notes')?.touched && form.get('notes')?.hasError('maxlength')" class="text-xs text-red-600 dark:text-red-400 mt-1">Must be 1000 characters or fewer.</p>
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
            <span>{{ isSubmitting() ? 'Submitting...' : 'Report Incident' }}</span>
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
  private readonly notify = inject(NotificationService);

  form: FormGroup;
  isSubmitting = signal(false);
  error = signal('');
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
      incidentDate: [toLocalIsoDate(), Validators.required],
      symptoms: ['', [Validators.required, Validators.maxLength(1000)]],
      affectedAnimalCount: [1, [Validators.required, Validators.min(1)]],
      notes: ['', Validators.maxLength(1000)]
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notify.error('Please fill in all required fields correctly.');
      return;
    }

    this.isSubmitting.set(true);
    this.error.set('');
    
    const formValue = this.form.value;
    const farmId = this.contextService.currentFarmValue?.id || '';

    if (!farmId) {
      const msg = 'No farm context available.';
      this.error.set(msg);
      this.notify.error(msg);
      this.isSubmitting.set(false);
      return;
    }

    const request = {
      farmId: farmId,
      ...formValue,
      severity: Number(formValue.severity)
    };

    this.healthService.reportIncident(request).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.notify.success('Incident reported successfully!');
        this.dialogRef.close(true);
      },
      error: (err) => {
        const parsedMsg = parseApiError(err, 'Failed to report incident. Please try again.');
        this.error.set(parsedMsg);
        this.notify.error(parsedMsg);
        this.isSubmitting.set(false);
      }
    });
  }
}
