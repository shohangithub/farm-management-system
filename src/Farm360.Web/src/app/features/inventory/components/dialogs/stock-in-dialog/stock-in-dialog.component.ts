import { Component, ChangeDetectionStrategy, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { InventoryItem, Supplier } from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-stock-in-dialog',
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
          <div class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
            <mat-icon class="!w-5 !h-5 !text-[20px]">add_business</mat-icon>
          </div>
          <span>Record Stock In Receipt</span>
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
                <mat-option [value]="item.id">{{ item.name }} (Current: {{ item.currentStock }} {{ item.unitOfMeasure }})</mat-option>
              }
            </mat-select>
            <mat-error>Item selection is required</mat-error>
          </mat-form-field>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Received Quantity</mat-label>
              <input matInput type="number" formControlName="quantity" step="1" min="0.1" required />
              <mat-error>Quantity is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Unit Cost (BDT)</mat-label>
              <input matInput type="number" formControlName="unitCostBdt" step="0.5" min="0" required />
              <mat-error>Unit cost is required</mat-error>
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Supplier</mat-label>
              <mat-select formControlName="supplierId">
                <mat-option [value]="null">None / Direct Purchase</mat-option>
                @for (sup of suppliers(); track sup.id) {
                  <mat-option [value]="sup.id">{{ sup.name }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Purchase Date</mat-label>
              <input matInput type="date" formControlName="transactionDate" required />
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Invoice #</mat-label>
              <input matInput formControlName="invoiceNumber" placeholder="INV-9910" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Batch / Lot #</mat-label>
              <input matInput formControlName="batchNumber" placeholder="LOT-2026A" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Expiry Date</mat-label>
              <input matInput type="date" formControlName="expiryDate" />
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Notes / Comments</mat-label>
            <textarea matInput formControlName="notes" rows="2"></textarea>
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
          <span>Record Stock In</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockInDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<StockInDialogComponent>);
  readonly data = inject<{ item?: InventoryItem; farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly availableItems = signal<InventoryItem[]>([]);
  readonly suppliers = signal<Supplier[]>([]);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = this.fb.group({
    inventoryItemId: [this.data?.item?.id || '', [Validators.required]],
    quantity: [null as number | null, [Validators.required, Validators.min(0.1)]],
    unitCostBdt: [this.data?.item?.weightedAverageCostBdt || null as number | null, [Validators.required, Validators.min(0)]],
    supplierId: [null as string | null],
    transactionDate: [new Date().toISOString().split('T')[0], [Validators.required]],
    invoiceNumber: [''],
    batchNumber: [''],
    expiryDate: [''],
    notes: ['']
  });

  ngOnInit(): void {
    this.inventoryService.getItems({ farmId: this.data.farmId, pageSize: 100 }).subscribe({
      next: (res) => this.availableItems.set(res.items),
      error: (err: any) => this.error.set(parseApiError(err))
    });

    this.inventoryService.getSuppliers({ pageSize: 100 }).subscribe({
      next: (res) => this.suppliers.set(res.items),
      error: () => {}
    });

    this.form.get('inventoryItemId')?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(itemId => {
        if (itemId) {
          const selectedItem = this.availableItems().find(x => x.id === itemId);
          if (selectedItem) {
            this.form.patchValue({
              unitCostBdt: selectedItem.weightedAverageCostBdt > 0 ? selectedItem.weightedAverageCostBdt : null,
              quantity: null as any
            });
          }
        }
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
      supplierId: formVal.supplierId ? formVal.supplierId : null,
      invoiceNumber: formVal.invoiceNumber ? formVal.invoiceNumber : null,
      batchNumber: formVal.batchNumber ? formVal.batchNumber : null,
      expiryDate: formVal.expiryDate ? formVal.expiryDate : null,
      notes: formVal.notes ? formVal.notes : null
    };

    this.inventoryService.recordStockIn(request as any).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open('Stock In transaction recorded successfully.', 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
