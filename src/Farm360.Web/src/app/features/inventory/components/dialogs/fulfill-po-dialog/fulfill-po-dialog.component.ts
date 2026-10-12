import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { InventoryService } from '../../../services/inventory.service';
import { parseApiError } from '../../../../../core/utils/error-parser';
import {
  CreditTermsFieldsComponent,
  buildCreditTermsGroup,
  toCreditTerms,
} from '../../../../finance/components/credit-terms-fields/credit-terms-fields.component';

export interface FulfillPoDialogData {
  id: string;
  poNumber: string;
  supplierId?: string;
  totalAmountBdt: number;
}

/**
 * Receives a purchase order into stock and records how it was paid: in full on receipt (cash), or
 * on credit — the supplier's bill then waits in Finance → Payables & Receivables.
 */
@Component({
  selector: 'app-fulfill-po-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, DecimalPipe, CreditTermsFieldsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-emerald-500">inventory</mat-icon>
            Receive PO #{{ data.poNumber }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">All items go into stock and the inventory balance is updated.</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <div class="p-6 space-y-4 overflow-y-auto">
        @if (error()) {
          <div class="p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm">{{ error() }}</div>
        }
        <div class="flex items-center justify-between p-4 rounded-xl bg-gray-50 dark:bg-gray-800/60 border border-gray-200 dark:border-gray-700">
          <div>
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Supplier</div>
            <div class="text-sm font-bold text-gray-900 dark:text-white">{{ supplierName() || '—' }}</div>
          </div>
          <div class="text-right">
            <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">Order total</div>
            <div class="text-lg font-black text-gray-900 dark:text-white">৳ {{ data.totalAmountBdt | number: '1.2-2' }}</div>
          </div>
        </div>

        <app-credit-terms-fields [group]="credit" kind="Payable" [total]="data.totalAmountBdt"
          [requireParty]="false" [partyPlaceholder]="supplierName() || 'Supplier'"></app-credit-terms-fields>
      </div>

      <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3">
        <button type="button" mat-dialog-close [disabled]="busy()"
          class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50">
          Cancel
        </button>
        <button type="button" (click)="fulfill()" [disabled]="busy() || credit.invalid"
          class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 rounded-xl hover:bg-emerald-700 disabled:opacity-50 flex items-center gap-2">
          @if (busy()) { <mat-icon class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon> }
          {{ busy() ? 'Receiving…' : 'Fulfill & Receive Stock' }}
        </button>
      </div>
    </div>
  `,
})
export class FulfillPoDialogComponent {
  readonly data = inject<FulfillPoDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<FulfillPoDialogComponent>);
  private readonly inventory = inject(InventoryService);

  readonly credit = buildCreditTermsGroup(inject(FormBuilder), false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly supplierName = signal<string | null>(null);

  constructor() {
    if (this.data.supplierId) {
      this.inventory.getSuppliers({ pageSize: 100 }).subscribe({
        next: res => this.supplierName.set(res.items.find(s => s.id === this.data.supplierId)?.name ?? null),
        error: () => this.supplierName.set(null),
      });
    }
  }

  fulfill(): void {
    const credit = toCreditTerms(this.credit.getRawValue());
    if (credit && (credit.paidNowBdt ?? 0) >= this.data.totalAmountBdt) {
      this.error.set('The amount paid now covers the whole order — choose “Paid in full now” instead.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.inventory.fulfillPurchaseOrder(this.data.id, credit).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.busy.set(false);
        this.error.set(parseApiError(err, 'Failed to fulfill the purchase order.'));
      },
    });
  }
}
