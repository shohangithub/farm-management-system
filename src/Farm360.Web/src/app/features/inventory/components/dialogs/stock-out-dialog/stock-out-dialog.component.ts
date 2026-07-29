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
import { InventoryService } from '../../../services/inventory.service';
import { InventoryItem, StockTransactionType, StockTransactionTypeNames } from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-stock-out-dialog',
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
          <div class="w-8 h-8 rounded-lg bg-amber-50 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">remove_shopping_cart</mat-icon>
          </div>
          <span>Record Stock Out / Write-Off</span>
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
            <mat-label>Inventory Item</mat-label>
            <mat-select formControlName="inventoryItemId" required>
              @for (item of availableItems(); track item.id) {
                <mat-option [value]="item.id">{{ item.name }} (Available: {{ item.currentStock }} {{ item.unitOfMeasure }})</mat-option>
              }
            </mat-select>
            <mat-error>Item selection is required</mat-error>
          </mat-form-field>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Deduction Type</mat-label>
              <mat-select formControlName="transactionType" required>
                <mat-option [value]="StockTransactionType.ManualStockOut">Manual Stock Out</mat-option>
                <mat-option [value]="StockTransactionType.WriteOff">Stock Write-Off (Damaged/Expired)</mat-option>
                <mat-option [value]="StockTransactionType.Adjustment">Stock Adjustment</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Deducted Quantity</mat-label>
              <input matInput type="number" formControlName="quantity" step="1" min="0.1" required />
              <mat-error>Quantity is required</mat-error>
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Transaction Date</mat-label>
            <input matInput type="date" formControlName="transactionDate" required />
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Reason / Comments</mat-label>
            <textarea matInput formControlName="reason" rows="2" placeholder="e.g. Expired batch discarded or damaged bag"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-amber-600 hover:bg-amber-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>Deduct Stock</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockOutDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<StockOutDialogComponent>);
  readonly data = inject<{ item?: InventoryItem; farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly availableItems = signal<InventoryItem[]>([]);
  readonly StockTransactionType = StockTransactionType;

  readonly form = this.fb.group({
    inventoryItemId: [this.data?.item?.id || '', [Validators.required]],
    transactionType: [StockTransactionType.ManualStockOut, [Validators.required]],
    quantity: [1, [Validators.required, Validators.min(0.1)]],
    transactionDate: [new Date().toISOString().split('T')[0], [Validators.required]],
    reason: ['']
  });

  ngOnInit(): void {
    this.inventoryService.getItems({ farmId: this.data.farmId, pageSize: 100 }).subscribe({
      next: (res) => this.availableItems.set(res.items.filter(i => i.currentStock > 0)),
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
      reason: formVal.reason ? formVal.reason : null
    };

    this.inventoryService.recordStockOut(request as any).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open('Stock deduction recorded successfully.', 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
