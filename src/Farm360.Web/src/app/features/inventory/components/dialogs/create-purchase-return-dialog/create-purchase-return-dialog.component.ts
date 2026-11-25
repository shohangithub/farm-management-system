import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { InventoryService } from '../../../services/inventory.service';
import {
  ReturnablePoItem,
  PurchaseReturnReason,
  PurchaseReturnReasonNames,
  CreatePurchaseReturnRequest,
  CreatePurchaseReturnItemRequest
} from '../../../models/inventory.models';
import { toLocalIsoDate } from '../../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../../core/i18n/i18n.service';

export interface CreatePurchaseReturnDialogData {
  purchaseOrderId: string;
  poNumber: string;
  farmId: string;
}

@Component({
  selector: 'app-create-purchase-return-dialog',
  standalone: true,
  imports: [MoneyPipe, 
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule,
    TranslatePipe
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full max-w-4xl">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[22px] !w-[22px] !h-[22px] text-amber-500">assignment_return</mat-icon>
            {{ 'Return Items to Supplier' | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
            {{ 'Purchase Order:' | t }} <span class="font-semibold text-gray-700 dark:text-gray-200">#{{ data.poNumber }}</span>
          </p>
        </div>
        <button [attr.aria-label]="'Close' | t" mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form & Body -->
      <form [formGroup]="form" class="flex flex-col overflow-hidden flex-1">
        <!-- Error Banner -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-5 overflow-y-auto custom-scrollbar flex-1">
          <!-- Return Header Details -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                {{ 'Return Date' | t }} <span class="text-red-500">*</span>
              </label>
              <input type="date" formControlName="returnDate"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-amber-500 focus:border-amber-500">
<p *ngIf="form.get('returnDate')?.invalid && form.get('returnDate')?.touched" class="mt-1 text-xs text-red-600 dark:text-red-400" role="alert">{{ fieldError(form, 'returnDate') }}</p>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                {{ 'Reason' | t }} <span class="text-red-500">*</span>
              </label>
              <select formControlName="reason"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-amber-500 focus:border-amber-500">
                <option *ngFor="let r of reasonOptions" [value]="r.value">{{ r.label | t }}</option>
              </select>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                {{ 'Credit Note / Ref #' | t }}
              </label>
              <input type="text" formControlName="creditNoteNumber" [placeholder]="'e.g. CN-8821' | t"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-amber-500 focus:border-amber-500">
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
              {{ 'Notes / Remarks' | t }}
            </label>
            <textarea formControlName="notes" rows="2" [placeholder]="'Describe the reason for returning goods...' | t"
              class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-amber-500 focus:border-amber-500"></textarea>
          </div>

          <!-- Items Table -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-700 dark:text-gray-300">
                {{ 'Items to Return' | t }}
              </label>
              <span class="text-xs text-gray-500">{{ 'Enter quantity for the items you want to return' | t }}</span>
            </div>

            <div *ngIf="isLoadingItems()" class="py-12 flex justify-center items-center text-gray-500">
              <mat-icon class="animate-spin mr-2">autorenew</mat-icon>
              <span>{{ 'Loading returnable items...' | t }}</span>
            </div>

            <div *ngIf="!isLoadingItems() && items().length === 0" class="p-6 text-center text-gray-500 bg-gray-50 dark:bg-gray-900/50 rounded-xl border border-dashed border-gray-200 dark:border-gray-700">
              <mat-icon class="text-gray-400 !text-32px !w-8 !h-8 mb-1">info</mat-icon>
              <p class="text-sm font-medium">{{ 'No items are available to return for this purchase order.' | t }}</p>
              <p class="text-xs text-gray-400">{{ 'All items may have already been returned or current stock is zero.' | t }}</p>
            </div>

            <div *ngIf="!isLoadingItems() && items().length > 0" class="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
              <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800 text-xs font-bold text-gray-500 uppercase tracking-wider">
                  <tr>
                    <th class="px-3 py-2.5 text-left">{{ 'Item' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Ordered' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Returned' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Stock' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Max Return' | t }}</th>
                    <th class="px-3 py-2.5 text-center w-32">{{ 'Return Qty' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Unit Price' | t }}</th>
                    <th class="px-3 py-2.5 text-right">{{ 'Subtotal' | t }}</th>
                  </tr>
                </thead>
                <tbody formArrayName="items" class="divide-y divide-gray-200 dark:divide-gray-700 bg-white dark:bg-gray-800">
                  <tr *ngFor="let itemGroup of itemControls; let i = index" [formGroupName]="i" class="hover:bg-gray-50/50 dark:hover:bg-gray-700/50">
                    <td class="px-3 py-2 font-medium text-gray-900 dark:text-white">
                      {{ items()[i].itemName }}
                      <span class="text-xs text-gray-400 block">{{ items()[i].unitOfMeasure }}</span>
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ items()[i].orderedQuantity }}
                      <span *ngIf="items()[i].enteredUnitName" class="block text-[11px] text-gray-400">
                        ({{ items()[i].enteredQuantity | number:'1.0-4' }} × {{ items()[i].enteredUnitName }})
                      </span>
                    </td>
                    <td class="px-3 py-2 text-right text-gray-500">
                      {{ items()[i].alreadyReturnedQuantity }}
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ items()[i].currentStock }}
                    </td>
                    <td class="px-3 py-2 text-right font-semibold text-amber-600 dark:text-amber-400">
                      {{ items()[i].maxReturnableQuantity }}
                    </td>
                    <td class="px-3 py-2 text-center">
                      <input type="number" formControlName="quantity" min="0" [max]="items()[i].maxReturnableQuantity" step="any"
                        class="w-24 px-2 py-1 text-right text-sm border rounded-lg focus:ring-2 focus:ring-amber-500 focus:border-amber-500 bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                        [ngClass]="{'border-red-500': itemGroup.get('quantity')?.invalid}">
                    </td>
                    <td class="px-3 py-2 text-right text-gray-600 dark:text-gray-300">
                      {{ items()[i].unitCostBdt | money:2 }}
                    </td>
                    <td class="px-3 py-2 text-right font-bold text-gray-900 dark:text-white">
                      {{ getLineTotal(i) | money:2 }}
                    </td>
                  </tr>
                </tbody>
                <tfoot class="bg-gray-50/80 dark:bg-gray-900/40 border-t border-gray-200 dark:border-gray-700">
                  <tr>
                    <td colspan="7" class="px-3 py-2.5 text-right font-bold text-xs uppercase tracking-wider text-gray-700 dark:text-gray-300">
                      {{ 'Total Refund / Credit:' | t }}
                    </td>
                    <td class="px-3 py-2.5 text-right font-black text-amber-600 dark:text-amber-400 text-base">
                      {{ calculatedTotal() | money:2 }}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex items-center justify-between shrink-0">
          <div class="text-xs text-gray-500">
            {{ 'Selected for return:' | t }} <span class="font-bold text-gray-700 dark:text-gray-300">{{ '{n} item(s)' | t: { n: selectedItemsCount() } }}</span>
          </div>

          <div class="flex items-center gap-3">
            <button type="button" mat-dialog-close [disabled]="isLoading()"
              class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
              {{ 'Cancel' | t }}
            </button>
            <button type="button" (click)="onSubmit(false)" [disabled]="isSubmitDisabled()"
              class="px-4 py-2 text-sm font-semibold text-gray-800 dark:text-gray-200 bg-gray-200 dark:bg-gray-700 hover:bg-gray-300 dark:hover:bg-gray-600 rounded-xl transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed">
              {{ 'Save Draft' | t }}
            </button>
            <button type="button" (click)="onSubmit(true)" [disabled]="isSubmitDisabled()"
              class="px-4 py-2 text-sm font-semibold text-white bg-amber-600 hover:bg-amber-700 rounded-xl transition-colors shadow-sm shadow-amber-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
              <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
              <span>{{ (isLoading() ? 'Processing...' : 'Complete Return') | t }}</span>
            </button>
          </div>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .custom-scrollbar::-webkit-scrollbar { width: 6px; }
    .custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
    .custom-scrollbar::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.5); border-radius: 20px; }
    .custom-scrollbar:hover::-webkit-scrollbar-thumb { background-color: rgba(156, 163, 175, 0.8); }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreatePurchaseReturnDialogComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<CreatePurchaseReturnDialogComponent>);
  readonly data: CreatePurchaseReturnDialogData = inject(MAT_DIALOG_DATA);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(I18nService);

  readonly isLoading = signal(false);
  readonly isLoadingItems = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<ReturnablePoItem[]>([]);

  readonly reasonOptions = [
    { value: PurchaseReturnReason.Damaged, label: PurchaseReturnReasonNames[PurchaseReturnReason.Damaged] },
    { value: PurchaseReturnReason.Expired, label: PurchaseReturnReasonNames[PurchaseReturnReason.Expired] },
    { value: PurchaseReturnReason.WrongItem, label: PurchaseReturnReasonNames[PurchaseReturnReason.WrongItem] },
    { value: PurchaseReturnReason.QualityIssue, label: PurchaseReturnReasonNames[PurchaseReturnReason.QualityIssue] },
    { value: PurchaseReturnReason.Excess, label: PurchaseReturnReasonNames[PurchaseReturnReason.Excess] },
    { value: PurchaseReturnReason.Other, label: PurchaseReturnReasonNames[PurchaseReturnReason.Other] }
  ];

  readonly form: FormGroup = this.fb.group({
    returnDate: [toLocalIsoDate(), Validators.required],
    reason: [PurchaseReturnReason.Damaged, Validators.required],
    creditNoteNumber: [''],
    notes: [''],
    items: this.fb.array([])
  });

  get itemsFormArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  get itemControls(): FormGroup[] {
    return this.itemsFormArray.controls as FormGroup[];
  }

  ngOnInit(): void {
    this.loadReturnableItems();
  }

  private loadReturnableItems(): void {
    this.isLoadingItems.set(true);
    this.error.set(null);

    this.inventoryService.getReturnablePoItems(this.data.purchaseOrderId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (returnableItems) => {
          this.items.set(returnableItems);
          this.initItemsForm(returnableItems);
          this.isLoadingItems.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.detail || err?.message || this.i18n.t('Failed to load returnable items.'));
          this.isLoadingItems.set(false);
        }
      });
  }

  private initItemsForm(items: ReturnablePoItem[]): void {
    this.itemsFormArray.clear();
    for (const item of items) {
      const group = this.fb.group({
        purchaseOrderItemId: [item.purchaseOrderItemId],
        quantity: [0, [Validators.min(0), Validators.max(item.maxReturnableQuantity)]]
      });
      this.itemsFormArray.push(group);
    }
  }

  getLineTotal(index: number): number {
    const qty = Number(this.itemControls[index]?.get('quantity')?.value) || 0;
    const item = this.items()[index];
    return item ? qty * item.unitCostBdt : 0;
  }

  readonly calculatedTotal = computed(() => {
    // Computed based on items list and form array
    let sum = 0;
    const currentItems = this.items();
    for (let i = 0; i < currentItems.length; i++) {
      const qty = Number(this.itemControls[i]?.get('quantity')?.value) || 0;
      sum += qty * currentItems[i].unitCostBdt;
    }
    return sum;
  });

  readonly selectedItemsCount = computed(() => {
    let count = 0;
    for (const ctrl of this.itemControls) {
      const qty = Number(ctrl.get('quantity')?.value) || 0;
      if (qty > 0) count++;
    }
    return count;
  });

  isSubmitDisabled(): boolean {
    if (this.isLoading() || this.form.invalid) return true;
    let hasAnyQuantity = false;
    for (const ctrl of this.itemControls) {
      const qty = Number(ctrl.get('quantity')?.value) || 0;
      if (qty > 0) {
        hasAnyQuantity = true;
        break;
      }
    }
    return !hasAnyQuantity;
  }

  onSubmit(completeImmediately: boolean): void {
    if (this.isSubmitDisabled()) return;

    this.isLoading.set(true);
    this.error.set(null);

    const formVal = this.form.value;
    const returnItems: CreatePurchaseReturnItemRequest[] = [];

    for (let i = 0; i < this.itemControls.length; i++) {
      const qty = Number(this.itemControls[i].get('quantity')?.value) || 0;
      if (qty > 0) {
        returnItems.push({
          purchaseOrderItemId: this.items()[i].purchaseOrderItemId,
          quantity: qty
        });
      }
    }

    const payload: CreatePurchaseReturnRequest = {
      farmId: this.data.farmId,
      purchaseOrderId: this.data.purchaseOrderId,
      returnDate: formVal.returnDate,
      reason: formVal.reason,
      creditNoteNumber: formVal.creditNoteNumber ? formVal.creditNoteNumber.trim() : null,
      notes: formVal.notes ? formVal.notes.trim() : null,
      items: returnItems
    };

    this.inventoryService.createPurchaseReturn(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (completeImmediately) {
            this.inventoryService.completePurchaseReturn(res.id)
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe({
                next: () => {
                  this.isLoading.set(false);
                  this.dialogRef.close({ success: true, completed: true, returnId: res.id });
                },
                error: (err) => {
                  this.isLoading.set(false);
                  this.error.set(err?.error?.detail || err?.message || this.i18n.t('Return was created as draft, but completing it failed.'));
                }
              });
          } else {
            this.isLoading.set(false);
            this.dialogRef.close({ success: true, completed: false, returnId: res.id });
          }
        },
        error: (err) => {
          this.isLoading.set(false);
          this.error.set(err?.error?.detail || err?.message || this.i18n.t('Failed to create purchase return.'));
        }
      });
  }

  /** Human-readable message for the first validation error on a control. */
  fieldError(group: { get(name: string): import('@angular/forms').AbstractControl | null }, name: string): string {
    const e = group.get(name)?.errors;
    if (!e) return '';
    if (e['required']) return this.i18n.t('This field is required.');
    if (e['min']) return this.i18n.t('Must be at least {n}.', { n: e['min'].min });
    if (e['max']) return this.i18n.t('Must be at most {n}.', { n: e['max'].max });
    if (e['maxlength']) return this.i18n.t('Must be {n} characters or fewer.', { n: e['maxlength'].requiredLength });
    if (e['minlength']) return this.i18n.t('Must be at least {n} characters.', { n: e['minlength'].requiredLength });
    if (e['email']) return this.i18n.t('Enter a valid email address.');
    if (e['pattern']) return this.i18n.t('Invalid format.');
    return this.i18n.t('Invalid value.');
  }
}
