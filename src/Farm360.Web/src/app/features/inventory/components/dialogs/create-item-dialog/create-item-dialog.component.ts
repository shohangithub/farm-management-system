import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
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
import { InventoryService } from '../../../services/inventory.service';
import { InventoryCategory, InventoryCategoryNames, InventoryItem } from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-create-item-dialog',
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
            <mat-icon class="!w-5 !h-5 !text-[20px]">inventory_2</mat-icon>
          </div>
          <span>{{ isEdit ? 'Edit Inventory Item' : 'Add New Inventory Item' }}</span>
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
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Item Name</mat-label>
              <input matInput formControlName="name" placeholder="e.g. Oxytetracycline Injection" required />
              <mat-error>Name is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Category</mat-label>
              <mat-select formControlName="category" required>
                @for (cat of categories; track cat.value) {
                  <mat-option [value]="cat.value">{{ cat.label }}</mat-option>
                }
              </mat-select>
              <mat-error>Category is required</mat-error>
            </mat-form-field>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Unit of Measure</mat-label>
              <input matInput formControlName="unitOfMeasure" placeholder="e.g. kg, vial, bottle, dose" required />
              <mat-error>Unit is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Reorder Threshold</mat-label>
              <input matInput type="number" formControlName="reorderThreshold" step="1" min="0" required />
              <mat-error>Threshold is required</mat-error>
            </mat-form-field>
          </div>

          @if (!isEdit) {
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Initial Stock Qty</mat-label>
                <input matInput type="number" formControlName="initialStock" step="1" min="0" />
              </mat-form-field>

              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Initial Unit Cost (BDT)</mat-label>
                <input matInput type="number" formControlName="initialCostBdt" step="0.5" min="0" />
              </mat-form-field>
            </div>
          }

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>SKU (Auto-generated if empty)</mat-label>
              <input matInput formControlName="sku" placeholder="e.g. MED-8910" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Storage Location</mat-label>
              <input matInput formControlName="storageLocation" placeholder="e.g. Cold Storage Shelf B" />
            </mat-form-field>
          </div>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2">
        <button class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors" [disabled]="isSubmitting()" (click)="dialogRef.close()">
          Cancel
        </button>
        <button class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 rounded-lg transition-colors shadow-sm inline-flex items-center gap-1.5 disabled:opacity-50" [disabled]="form.invalid || isSubmitting()" (click)="onSubmit()">
          <mat-spinner *ngIf="isSubmitting()" diameter="16"></mat-spinner>
          <span>{{ isEdit ? 'Update Item' : 'Save Item' }}</span>
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateItemDialogComponent {
  readonly dialogRef = inject(MatDialogRef<CreateItemDialogComponent>);
  readonly data = inject<{ item?: InventoryItem; farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly isEdit = !!this.data?.item;

  readonly categories = [
    { value: InventoryCategory.Feed, label: InventoryCategoryNames[InventoryCategory.Feed] },
    { value: InventoryCategory.Medicine, label: InventoryCategoryNames[InventoryCategory.Medicine] },
    { value: InventoryCategory.Vaccine, label: InventoryCategoryNames[InventoryCategory.Vaccine] },
    { value: InventoryCategory.Chemical, label: InventoryCategoryNames[InventoryCategory.Chemical] },
    { value: InventoryCategory.Equipment, label: InventoryCategoryNames[InventoryCategory.Equipment] },
    { value: InventoryCategory.Other, label: InventoryCategoryNames[InventoryCategory.Other] },
  ];

  readonly form = this.fb.group({
    name: [this.data?.item?.name || '', [Validators.required, Validators.maxLength(200)]],
    category: [this.data?.item?.category || InventoryCategory.Feed, [Validators.required]],
    unitOfMeasure: [this.data?.item?.unitOfMeasure || 'kg', [Validators.required, Validators.maxLength(30)]],
    reorderThreshold: [this.data?.item?.reorderThreshold ?? 50, [Validators.required, Validators.min(0)]],
    sku: [this.data?.item?.sku || ''],
    initialStock: [this.data?.item?.currentStock ?? 0, [Validators.min(0)]],
    initialCostBdt: [this.data?.item?.weightedAverageCostBdt ?? 0, [Validators.min(0)]],
    storageLocation: [this.data?.item?.storageLocation || '']
  });

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const val = {
      ...formVal,
      farmId: this.data.farmId,
      sku: formVal.sku ? formVal.sku : null,
      storageLocation: formVal.storageLocation ? formVal.storageLocation : null
    };

    const request$: Observable<any> = this.isEdit && this.data.item
      ? this.inventoryService.updateItem(this.data.item.id, val as any)
      : this.inventoryService.createItem(val as any);

    request$.subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.snackBar.open(`Inventory item ${this.isEdit ? 'updated' : 'created'} successfully.`, 'OK', { duration: 4000 });
        this.dialogRef.close(true);
      },
      error: (err: any) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
