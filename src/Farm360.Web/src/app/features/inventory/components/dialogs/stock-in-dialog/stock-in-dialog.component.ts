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
import { UnitPickerComponent, UnitSelection } from '../../unit-picker/unit-picker.component';
import {
  CreditTermsFieldsComponent,
  buildCreditTermsGroup,
  toCreditTerms,
} from '../../../../finance/components/credit-terms-fields/credit-terms-fields.component';

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
    MatProgressSpinnerModule,
    CreditTermsFieldsComponent,
    UnitPickerComponent
  ],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl overflow-hidden max-w-xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between shrink-0">
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

      <!-- Content (scrolls) -->
      <div class="p-6 flex-1 min-h-0 overflow-y-auto">
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

          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Quantity</mat-label>
              <input matInput type="number" formControlName="quantity" step="any" min="0" required />
              <mat-error>Quantity is required</mat-error>
            </mat-form-field>

            <!-- Bought in bags, bottles…: stock is kept in the item's base unit -->
            <app-unit-picker appearance="material" label="Unit" [itemId]="form.value.inventoryItemId" [quantity]="form.value.quantity"
              (selectionChange)="onUnit($event)"></app-unit-picker>

            <mat-form-field appearance="outline" class="w-full">
              <mat-label>Cost / {{ unit()?.name || 'unit' }} (৳)</mat-label>
              <input matInput type="number" formControlName="unitCostBdt" step="any" min="0" required />
              <mat-hint *ngIf="purchaseTotal() as t">Total ৳{{ t | number:'1.0-2' }}</mat-hint>
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

          <!-- Decides the accounting entry: a paid purchase moves cash into stock; opening or
               owner-supplied stock is the owner's capital. Neither is an expense until used. -->
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>How was this stock paid for?</mat-label>
            <mat-select formControlName="fundingSource">
              <mat-option value="CashPurchase">Bought by the farm (paid now or on credit)</mat-option>
              <mat-option value="OwnerContribution">Opening stock / brought in by owner (no farm payment)</mat-option>
            </mat-select>
            <mat-hint>Stock is an asset in the books; it becomes a cost when it is fed, used or written off.</mat-hint>
          </mat-form-field>

          @if (form.value.fundingSource === 'CashPurchase') {
            <app-credit-terms-fields [group]="credit" kind="Payable" [total]="purchaseTotal()"
              [requireParty]="false" partyPlaceholder="Selected supplier"></app-credit-terms-fields>
          }

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>Notes / Comments</mat-label>
            <textarea matInput formControlName="notes" rows="2"></textarea>
          </mat-form-field>
        </form>
      </div>

      <!-- Actions -->
      <div class="px-6 py-4 bg-gray-50/50 dark:bg-gray-900/30 border-t border-gray-100 dark:border-gray-800 flex justify-end gap-2 shrink-0">
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
  /** The unit the quantity and cost are entered in (e.g. "Bag (50 kg)"). */
  readonly unit = signal<UnitSelection | null>(null);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = this.fb.group({
    inventoryItemId: [this.data?.item?.id || '', [Validators.required]],
    quantity: [null as number | null, [Validators.required, Validators.min(0.0001)]],
    unitCostBdt: [this.data?.item?.weightedAverageCostBdt || null as number | null, [Validators.required, Validators.min(0)]],
    supplierId: [null as string | null],
    transactionDate: [new Date().toISOString().split('T')[0], [Validators.required]],
    invoiceNumber: [''],
    batchNumber: [''],
    expiryDate: [''],
    notes: [''],
    fundingSource: ['CashPurchase']
  });

  /** Paid in full now, or on credit to the supplier (Accounts Payable). */
  readonly credit = buildCreditTermsGroup(this.fb, false);

  purchaseTotal(): number | null {
    const v = this.form.value;
    const total = (Number(v.quantity) || 0) * (Number(v.unitCostBdt) || 0);
    return total > 0 ? Math.round(total * 100) / 100 : null;
  }

  /** Switching unit re-prices from the item's average cost per base unit (e.g. ৳58/kg → ৳2,900 a 50 kg bag). */
  onUnit(sel: UnitSelection): void {
    const previous = this.unit();
    this.unit.set(sel);
    const item = this.availableItems().find(x => x.id === this.form.value.inventoryItemId);
    const cost = Number(this.form.value.unitCostBdt) || 0;
    if (previous && previous.factor !== sel.factor && cost > 0) {
      this.form.patchValue({ unitCostBdt: Math.round(cost / previous.factor * sel.factor * 100) / 100 });
    } else if (!previous && item && item.weightedAverageCostBdt > 0) {
      this.form.patchValue({ unitCostBdt: Math.round(item.weightedAverageCostBdt * sel.factor * 100) / 100 });
    }
  }

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
            this.unit.set(null);
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
    const credit = formVal.fundingSource === 'CashPurchase' ? toCreditTerms(this.credit.getRawValue()) : null;
    if (credit && !credit.partyName && !formVal.supplierId) {
      this.isSubmitting.set(false);
      this.error.set('Choose the supplier (or enter their name) for a purchase on credit.');
      return;
    }
    if (credit && (credit.paidNowBdt ?? 0) >= (this.purchaseTotal() ?? 0)) {
      this.isSubmitting.set(false);
      this.error.set('The amount paid now covers the whole purchase — choose “Paid in full now” instead.');
      return;
    }

    const request = {
      credit,
      ...formVal,
      farmId: this.data.farmId,
      supplierId: formVal.supplierId ? formVal.supplierId : null,
      invoiceNumber: formVal.invoiceNumber ? formVal.invoiceNumber : null,
      batchNumber: formVal.batchNumber ? formVal.batchNumber : null,
      expiryDate: formVal.expiryDate ? formVal.expiryDate : null,
      notes: formVal.notes ? formVal.notes : null,
      unitId: this.unit()?.unitId ?? null
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
