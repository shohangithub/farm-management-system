import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';

import { InventoryService } from '../../../services/inventory.service';
import { DailyConsumableEntry } from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-confirm-consumable-entry-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full max-w-md mx-auto">
      <!-- Header -->
      <div class="px-5 py-3.5 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/40 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
            <mat-icon class="!w-5 !h-5 !text-[20px]">check_circle</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white m-0 leading-tight">
              Confirm Consumable Usage
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              Record actual quantity used for {{ data.entry.itemName }}
            </p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
          <mat-icon class="!w-5 !h-5 !text-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col flex-1 min-h-0">
        @if (error()) {
          <div class="mx-5 mt-3 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium shrink-0 flex items-start gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 shrink-0">error</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <div class="p-5 space-y-4">
          <!-- Item Summary Badge -->
          <div class="p-3 bg-gray-50 dark:bg-gray-800/60 rounded-xl border border-gray-100 dark:border-gray-700/60 flex items-center justify-between text-xs">
            <div>
              <span class="text-gray-400 block text-[10px] uppercase font-bold">Planned Qty</span>
              <span class="font-bold text-gray-900 dark:text-white text-sm">
                {{ data.entry.expectedQuantity }} {{ data.entry.unitOfMeasure }}
              </span>
            </div>
            <div class="text-right">
              <span class="text-gray-400 block text-[10px] uppercase font-bold">Available Stock</span>
              <span class="font-bold" [ngClass]="data.entry.currentStock < data.entry.expectedQuantity ? 'text-red-500' : 'text-emerald-600 dark:text-emerald-400'">
                {{ data.entry.currentStock }} {{ data.entry.unitOfMeasure }}
              </span>
            </div>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Actual Quantity Used ({{ data.entry.unitOfMeasure }})</mat-label>
            <input matInput type="number" formControlName="actualQuantity" step="0.5" min="0" required />
            <mat-error>Valid quantity is required</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Adjustment Reason (if different from planned)</mat-label>
            <input matInput formControlName="adjustmentReason" placeholder="e.g. Extra deep cleaning shift" />
          </mat-form-field>
        </div>

        <div class="px-5 py-3 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/40 flex items-center justify-end gap-2.5 shrink-0">
          <button mat-dialog-close type="button" class="px-3.5 py-1.5 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-xl transition-colors">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
            class="px-4 py-1.5 text-xs font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl transition-all shadow-md shadow-emerald-500/20 inline-flex items-center gap-2 disabled:opacity-50">
            <mat-spinner *ngIf="isSubmitting()" diameter="14" class="!stroke-white"></mat-spinner>
            <span>Confirm Usage</span>
          </button>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmConsumableEntryDialogComponent {
  readonly dialogRef = inject(MatDialogRef<ConfirmConsumableEntryDialogComponent>);
  readonly data = inject<{ entry: DailyConsumableEntry }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');

  readonly form = this.fb.group({
    actualQuantity: [this.data.entry.actualQuantity ?? this.data.entry.expectedQuantity, [Validators.required, Validators.min(0)]],
    adjustmentReason: [this.data.entry.adjustmentReason || '']
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const val = this.form.getRawValue();
    this.inventoryService.confirmDailyConsumable({
      entryId: this.data.entry.id,
      actualQuantity: Number(val.actualQuantity),
      adjustmentReason: val.adjustmentReason?.trim() || undefined
    }).subscribe({
      next: () => {
        this.snackBar.open('Consumable usage confirmed & stock deducted.', 'Close', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err: unknown) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
