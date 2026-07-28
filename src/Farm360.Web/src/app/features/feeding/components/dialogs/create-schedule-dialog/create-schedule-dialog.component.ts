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
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { FeedingService } from '../../../services/feeding.service';
import { FeedFormula, FeedingSchedule, ScheduleFrequency, ScheduleFrequencyNames } from '../../../models/feeding.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-schedule-dialog',
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
    MatProgressSpinnerModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden max-w-xl">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2">
          <div class="w-8 h-8 rounded-lg bg-blue-50 dark:bg-blue-950/50 text-blue-600 dark:text-blue-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">schedule</mat-icon>
          </div>
          <span>{{ isEdit ? 'Edit Feeding Schedule' : 'Assign Feeding Schedule' }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200">
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
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Schedule Title / Name</mat-label>
            <input matInput formControlName="title" placeholder="e.g. Shed A Morning Ration" required />
            <mat-error>Title is required</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Feed Formula</mat-label>
            <mat-select formControlName="formulaId" required>
              @for (formula of availableFormulas(); track formula.id) {
                <mat-option [value]="formula.id">{{ formula.title }} ({{ formula.totalCostPerKgBdt }} BDT/kg)</mat-option>
              }
            </mat-select>
            <mat-error>Formula is required</mat-error>
          </mat-form-field>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Target Qty (kg / head / day)</mat-label>
              <input matInput type="number" formControlName="targetQuantityKgPerHead" step="0.5" required />
              <mat-error>Target quantity is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Feeding Frequency</mat-label>
              <mat-select formControlName="frequency" required>
                @for (freq of frequencies; track freq.value) {
                  <mat-option [value]="freq.value">{{ freq.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Start Date</mat-label>
              <input matInput type="date" formControlName="startDate" required />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>End Date (Optional)</mat-label>
              <input matInput type="date" formControlName="endDate" />
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Instructions / Notes</mat-label>
            <textarea matInput formControlName="notes" rows="2" placeholder="e.g. Soak concentrate in warm water 30 mins before feeding"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ isEdit ? 'Update Schedule' : 'Assign Schedule' }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateScheduleDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<CreateScheduleDialogComponent>);
  readonly data = inject<{ schedule?: FeedingSchedule; farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly feedingService = inject(FeedingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly availableFormulas = signal<FeedFormula[]>([]);
  readonly isEdit = !!this.data.schedule;

  readonly frequencies = [
    { value: ScheduleFrequency.OnceDaily, label: ScheduleFrequencyNames[ScheduleFrequency.OnceDaily] },
    { value: ScheduleFrequency.TwiceDaily, label: ScheduleFrequencyNames[ScheduleFrequency.TwiceDaily] },
    { value: ScheduleFrequency.ThriceDaily, label: ScheduleFrequencyNames[ScheduleFrequency.ThriceDaily] },
    { value: ScheduleFrequency.AdLibitum, label: ScheduleFrequencyNames[ScheduleFrequency.AdLibitum] },
  ];

  readonly form = this.fb.group({
    title: [this.data.schedule?.title || '', [Validators.required, Validators.maxLength(200)]],
    formulaId: [this.data.schedule?.formulaId || '', [Validators.required]],
    targetQuantityKgPerHead: [this.data.schedule?.targetQuantityKgPerHead ?? 12.0, [Validators.required, Validators.min(0.1)]],
    frequency: [this.data.schedule?.frequency || ScheduleFrequency.TwiceDaily, [Validators.required]],
    startDate: [this.data.schedule?.startDate || new Date().toISOString().split('T')[0], [Validators.required]],
    endDate: [this.data.schedule?.endDate || ''],
    notes: [this.data.schedule?.notes || '']
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
    const val = {
      ...formVal,
      farmId: this.data.farmId,
      endDate: formVal.endDate ? formVal.endDate : null,
      notes: formVal.notes ? formVal.notes : null
    };

    const request$: Observable<any> = this.isEdit && this.data.schedule
      ? this.feedingService.updateSchedule(this.data.schedule.id, val as any)
      : this.feedingService.createSchedule(val as any);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Feeding schedule ${this.isEdit ? 'updated' : 'assigned'} successfully.`, 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
