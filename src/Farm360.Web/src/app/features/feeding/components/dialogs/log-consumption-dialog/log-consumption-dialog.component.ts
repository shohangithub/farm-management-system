import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FeedingService } from '../../../services/feeding.service';
import { FeedFormula } from '../../../models/feeding.models';
import { parseApiError } from '../../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../../shared/utils/local-date';
import { NotificationService } from '../../../../../shared/services/notification.service';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-log-consumption-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden max-w-xl">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">edit_note</mat-icon>
          </div>
          <span>{{ 'Log Daily Feed Consumption' | t }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200" [attr.aria-label]="'Close' | t">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6">
        @if (error()) {
          <div class="mb-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium">
            {{ error() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col gap-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>{{ 'Log Date' | t }}</mat-label>
              <input matInput type="date" formControlName="logDate" required />
              <mat-error>{{ 'Log date is required' | t }}</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>{{ 'Head Count (Animals)' | t }}</mat-label>
              <input matInput type="number" formControlName="headCount" min="1" required />
              <mat-error>{{ 'Head count is required' | t }}</mat-error>
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>{{ 'Feed Formula Used' | t }}</mat-label>
            <mat-select formControlName="formulaId" required>
              @for (formula of availableFormulas(); track formula.id) {
                <mat-option [value]="formula.id">{{ formula.title }} ({{ '{cost} BDT/kg' | t: { cost: formula.totalCostPerKgBdt } }})</mat-option>
              }
            </mat-select>
            <mat-error>{{ 'Formula selection is required' | t }}</mat-error>
          </mat-form-field>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>{{ 'Total Feed Offered' | t }}</mat-label>
              <input matInput type="number" formControlName="totalFeedOfferedKg" step="1" min="0" required />
              <span matTextSuffix class="text-gray-500 px-2 font-medium">{{ 'kg' | t }}</span>
              <mat-error>{{ 'Feed offered is required' | t }}</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>{{ 'Refusal / Wastage' | t }}</mat-label>
              <input matInput type="number" formControlName="totalRefusalKg" step="0.5" min="0" required />
              <span matTextSuffix class="text-gray-500 px-2 font-medium">{{ 'kg' | t }}</span>
              <mat-error>{{ 'Refusal is required' | t }}</mat-error>
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>{{ 'Observations / Feed Refusal Notes' | t }}</mat-label>
            <textarea matInput formControlName="notes" rows="2" [placeholder]="'e.g. Higher refusal due to humid weather' | t"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          {{ 'Cancel' | t }}
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ 'Log Consumption' | t }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LogConsumptionDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<LogConsumptionDialogComponent>);
  readonly data = inject<{ farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly feedingService = inject(FeedingService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(I18nService);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly availableFormulas = signal<FeedFormula[]>([]);

  readonly form = this.fb.group({
    logDate: [toLocalIsoDate(), [Validators.required]],
    headCount: [25, [Validators.required, Validators.min(1)]],
    formulaId: ['', [Validators.required]],
    totalFeedOfferedKg: [300, [Validators.required, Validators.min(0)]],
    totalRefusalKg: [12, [Validators.required, Validators.min(0)]],
    notes: ['']
  });

  ngOnInit(): void {
    this.feedingService.getFormulas(1, 100).subscribe({
      next: (res) => this.availableFormulas.set(res.items),
      error: (err: any) => this.error.set(parseApiError(err))
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const request = {
      ...formVal,
      farmId: this.data.farmId,
      notes: formVal.notes ? formVal.notes : null
    };

    this.feedingService.logConsumption(request as any).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.notify.success(this.i18n.t('Daily feed consumption logged successfully.'));
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
