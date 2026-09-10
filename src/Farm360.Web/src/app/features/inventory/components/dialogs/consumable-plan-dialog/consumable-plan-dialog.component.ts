import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InventoryService } from '../../../services/inventory.service';
import {
  ConsumableUsagePlan,
  InventoryItem,
  CreateConsumableUsagePlanRequest,
  UpdateConsumableUsagePlanRequest,
  ConsumablePlanItemInput,
  PagedResult
} from '../../../models/inventory.models';
import { parseApiError } from '../../../../../core/utils/error-parser';

@Component({
  selector: 'app-consumable-plan-dialog',
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
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] sm:max-h-[85vh] w-full max-w-2xl mx-auto">
      <!-- Header -->
      <div class="px-5 py-3.5 sm:px-6 sm:py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/40 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-teal-50 dark:bg-teal-950/50 text-teal-600 dark:text-teal-400 flex items-center justify-center shrink-0">
            <mat-icon class="!w-5 !h-5 !text-[20px]">cleaning_services</mat-icon>
          </div>
          <div>
            <h2 class="text-base sm:text-lg font-bold text-gray-900 dark:text-white m-0 leading-tight">
              {{ isEdit ? 'Edit Consumable Plan' : 'Create Daily Consumable Plan' }}
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              {{ isEdit ? 'Update recurring daily items and planned usage quantities' : 'Configure recurring daily supplies (shampoo, coils, Dettol, etc.)' }}
            </p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-1.5 -mr-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-full hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
          <mat-icon class="!w-5 !h-5 !text-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col flex-1 min-h-0 overflow-hidden">
        
        <!-- Error State -->
        @if (error()) {
          <div class="mx-4 mt-3 sm:mx-6 sm:mt-4 p-3 rounded-xl bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-xs border border-red-200 dark:border-red-800 font-medium shrink-0 flex items-start gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 shrink-0">error</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <!-- Scrollable Content -->
        <div class="p-4 sm:p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1 min-h-0">
          
          <!-- Plan Basic Details -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 sm:gap-4">
            <mat-form-field appearance="outline" class="w-full sm:col-span-2">
              <mat-label>Plan Name</mat-label>
              <input matInput formControlName="name" placeholder="e.g. Daily Cleaning & Hygiene Supplies" required />
              <mat-error>Plan name is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Start Date</mat-label>
              <input matInput type="date" formControlName="startDate" required />
              <mat-error>Start date is required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>End Date (Optional)</mat-label>
              <input matInput type="date" formControlName="endDate" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="w-full sm:col-span-2">
              <mat-label>Description / Notes</mat-label>
              <textarea matInput formControlName="description" rows="2" placeholder="e.g. Supplies used daily for shed sanitation, mosquito repellent coils, and animal bath shampoo"></textarea>
            </mat-form-field>
          </div>

          <!-- Items Sub-Form Array -->
          <div class="border-t border-gray-100 dark:border-gray-800 pt-4">
            <div class="flex items-center justify-between mb-3">
              <div>
                <h3 class="text-sm font-bold text-gray-900 dark:text-white m-0">Daily Consumable Items</h3>
                <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Select stock catalog items and expected daily consumption</p>
              </div>
              <button type="button" (click)="addItemRow()"
                class="px-3 py-1.5 text-xs font-semibold text-teal-700 dark:text-teal-300 bg-teal-50 dark:bg-teal-950/40 hover:bg-teal-100 dark:hover:bg-teal-900/50 rounded-lg border border-teal-200 dark:border-teal-800 transition-colors inline-flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]">add</mat-icon> Add Item
              </button>
            </div>

            <div formArrayName="items" class="space-y-3">
              @for (itemGroup of itemsFormArray.controls; track $index; let idx = $index) {
                <div [formGroupName]="idx" class="p-3 bg-gray-50 dark:bg-gray-800/40 rounded-xl border border-gray-200/80 dark:border-gray-700/60 relative group">
                  <div class="grid grid-cols-1 sm:grid-cols-12 gap-3 items-center">
                    
                    <!-- Item Selector (col 5) -->
                    <div class="sm:col-span-5">
                      <label class="block text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Catalog Item</label>
                      <select formControlName="inventoryItemId" (change)="onItemSelect(idx)"
                        class="w-full px-3 py-2 text-xs rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-teal-500">
                        <option value="" disabled>Select Item</option>
                        @for (inv of availableItems(); track inv.id) {
                          <option [value]="inv.id">{{ inv.name }} ({{ inv.unitOfMeasure }}) - Stock: {{ inv.currentStock }}</option>
                        }
                      </select>
                    </div>

                    <!-- Quantity Per Day (col 3) -->
                    <div class="sm:col-span-3">
                      <label class="block text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Qty / Day</label>
                      <input type="number" formControlName="plannedQuantityPerDay" step="0.5" min="0.1" placeholder="e.g. 2"
                        class="w-full px-3 py-2 text-xs rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-teal-500" />
                    </div>

                    <!-- Notes / Shift (col 3) -->
                    <div class="sm:col-span-3">
                      <label class="block text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Shift / Notes</label>
                      <input type="text" formControlName="notes" placeholder="e.g. Morning wash"
                        class="w-full px-3 py-2 text-xs rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-teal-500" />
                    </div>

                    <!-- Remove Action (col 1) -->
                    <div class="sm:col-span-1 flex items-end justify-center pt-2 sm:pt-4">
                      <button type="button" (click)="removeItemRow(idx)" [disabled]="itemsFormArray.length <= 1"
                        class="p-1 text-gray-400 hover:text-red-500 transition-colors disabled:opacity-30 disabled:cursor-not-allowed">
                        <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">delete</mat-icon>
                      </button>
                    </div>

                  </div>
                </div>
              }
            </div>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-5 py-3 sm:px-6 sm:py-3.5 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/40 flex items-center justify-end gap-3 shrink-0">
          <button mat-dialog-close type="button" class="px-4 py-2 text-xs font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-xl transition-colors">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
            class="px-5 py-2 text-xs font-semibold text-white bg-teal-600 hover:bg-teal-700 rounded-xl transition-all shadow-md shadow-teal-500/20 inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
            <mat-spinner *ngIf="isSubmitting()" diameter="16" class="!stroke-white"></mat-spinner>
            <span>{{ isEdit ? 'Update Plan' : 'Create Plan' }}</span>
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .custom-scrollbar {
      -webkit-overflow-scrolling: touch;
      overscroll-behavior: contain;
    }
    .custom-scrollbar::-webkit-scrollbar {
      width: 6px;
    }
    .custom-scrollbar::-webkit-scrollbar-track {
      background: transparent;
    }
    .custom-scrollbar::-webkit-scrollbar-thumb {
      background-color: rgba(156, 163, 175, 0.4);
      border-radius: 20px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConsumablePlanDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<ConsumablePlanDialogComponent>);
  readonly data = inject<{ plan?: ConsumableUsagePlan; farmId: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly inventoryService = inject(InventoryService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isSubmitting = signal(false);
  readonly error = signal('');
  readonly isEdit = !!this.data?.plan;
  readonly availableItems = signal<InventoryItem[]>([]);

  readonly form = this.fb.group({
    name: [this.data?.plan?.name || '', [Validators.required, Validators.maxLength(200)]],
    startDate: [this.data?.plan?.startDate || new Date().toISOString().substring(0, 10), [Validators.required]],
    endDate: [this.data?.plan?.endDate || ''],
    description: [this.data?.plan?.description || ''],
    items: this.fb.array([])
  });

  get itemsFormArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    // Load farm inventory items
    this.inventoryService.getItems({ farmId: this.data.farmId, pageSize: 100 }).subscribe({
      next: (res: PagedResult<InventoryItem>) => {
        this.availableItems.set(res.items);
      },
      error: () => {
        this.availableItems.set([]);
      }
    });

    if (this.data?.plan?.items && this.data.plan.items.length > 0) {
      for (const it of this.data.plan.items) {
        this.itemsFormArray.push(this.createItemFormGroup(it.inventoryItemId, it.plannedQuantityPerDay, it.notes));
      }
    } else {
      this.addItemRow();
    }
  }

  createItemFormGroup(inventoryItemId: string = '', plannedQty: number = 1, notes: string = ''): FormGroup {
    return this.fb.group({
      inventoryItemId: [inventoryItemId, [Validators.required]],
      plannedQuantityPerDay: [plannedQty, [Validators.required, Validators.min(0.01)]],
      notes: [notes || '']
    });
  }

  addItemRow(): void {
    this.itemsFormArray.push(this.createItemFormGroup());
  }

  removeItemRow(index: number): void {
    if (this.itemsFormArray.length > 1) {
      this.itemsFormArray.removeAt(index);
    }
  }

  onItemSelect(index: number): void {
    // Optional hook for unit of measure display
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set('');

    const formVal = this.form.getRawValue();
    const itemsPayload: ConsumablePlanItemInput[] = (formVal.items || []).map((i: any) => ({
      inventoryItemId: i.inventoryItemId,
      plannedQuantityPerDay: Number(i.plannedQuantityPerDay),
      notes: i.notes || null
    }));

    if (this.isEdit && this.data.plan) {
      const updateReq: UpdateConsumableUsagePlanRequest = {
        id: this.data.plan.id,
        name: formVal.name!.trim(),
        startDate: formVal.startDate!,
        endDate: formVal.endDate ? formVal.endDate : undefined,
        description: formVal.description?.trim() || undefined,
        items: itemsPayload
      };

      this.inventoryService.updateConsumablePlan(this.data.plan.id, updateReq).subscribe({
        next: () => {
          this.snackBar.open('Consumable plan updated successfully.', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err: unknown) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
    } else {
      const createReq: CreateConsumableUsagePlanRequest = {
        farmId: this.data.farmId,
        name: formVal.name!.trim(),
        startDate: formVal.startDate!,
        endDate: formVal.endDate ? formVal.endDate : undefined,
        description: formVal.description?.trim() || undefined,
        items: itemsPayload
      };

      this.inventoryService.createConsumablePlan(createReq).subscribe({
        next: () => {
          this.snackBar.open('Consumable plan created successfully.', 'Close', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: (err: unknown) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
    }
  }
}
