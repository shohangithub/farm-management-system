import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { CreditTerms, PAYMENT_METHODS } from '../../models/finance.model';

/**
 * The form group behind {@link CreditTermsFieldsComponent}.
 * @param requireParty false when the server can fill the party in (the PO's supplier, the sale's buyer).
 */
export function buildCreditTermsGroup(fb: FormBuilder, requireParty = true): FormGroup {
  const group = fb.group({
    onCredit: [false],
    partyName: [''],
    partyPhone: [''],
    dueDate: [''],
    paidNowBdt: [0, [Validators.min(0)]],
    paymentMethod: ['Cash'],
    notes: [''],
  });

  group.get('onCredit')!.valueChanges.subscribe(on => {
    const party = group.get('partyName')!;
    party.setValidators(on && requireParty ? [Validators.required] : []);
    party.updateValueAndValidity({ emitEvent: false });
  });

  return group;
}

/** The request payload, or null for a deal paid in full on the day. */
export function toCreditTerms(value: Record<string, unknown> | null | undefined): CreditTerms | null {
  if (!value?.['onCredit']) return null;
  const text = (key: string) => {
    const v = (value[key] as string | null | undefined)?.toString().trim();
    return v ? v : null;
  };
  return {
    partyName: text('partyName') ?? '',
    partyPhone: text('partyPhone'),
    dueDate: text('dueDate'),
    paidNowBdt: Number(value['paidNowBdt']) || 0,
    paymentMethod: text('paymentMethod'),
    notes: text('notes'),
  };
}

/**
 * "Paid now / On credit" switch with the credit terms. On credit, the cost or revenue is still
 * booked on the deal date, but cash only moves when the bill is paid in Payables & Receivables.
 */
@Component({
  selector: 'app-credit-terms-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [formGroup]="group" class="space-y-3">
      <div class="grid grid-cols-2 gap-1 p-1 rounded-xl bg-gray-100 dark:bg-gray-800 text-xs font-bold">
        <button type="button" (click)="set(false)"
          class="py-2 rounded-lg flex items-center justify-center gap-1.5 transition-colors"
          [ngClass]="!onCredit ? 'bg-white dark:bg-gray-700 text-emerald-700 dark:text-emerald-300 shadow-sm' : 'text-gray-500 dark:text-gray-400'">
          <mat-icon class="!w-4 !h-4 !text-[16px]">payments</mat-icon> {{ kind === 'Payable' ? 'Paid in full now' : 'Received in full now' }}
        </button>
        <button type="button" (click)="set(true)"
          class="py-2 rounded-lg flex items-center justify-center gap-1.5 transition-colors"
          [ngClass]="onCredit ? 'bg-white dark:bg-gray-700 text-amber-700 dark:text-amber-300 shadow-sm' : 'text-gray-500 dark:text-gray-400'">
          <mat-icon class="!w-4 !h-4 !text-[16px]">schedule</mat-icon> On credit (বাকি)
        </button>
      </div>

      @if (onCredit) {
        <div class="p-3 rounded-xl bg-amber-50/60 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800/50 space-y-3">
          <p class="text-[11px] text-amber-800 dark:text-amber-200 m-0 leading-snug">
            {{ kind === 'Payable'
              ? 'The cost is booked today; the unpaid balance is owed to the supplier (Accounts Payable) until you pay it in Finance → Payables & Receivables.'
              : 'The sale is booked today; the unpaid balance is owed by the customer (Accounts Receivable) until you collect it in Finance → Payables & Receivables.' }}
          </p>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">
                {{ kind === 'Payable' ? 'Supplier' : 'Customer' }} @if (requireParty) { <span class="text-red-500">*</span> }
              </label>
              <input type="text" formControlName="partyName" [placeholder]="partyPlaceholder"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
            <div class="space-y-1">
              <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">Phone</label>
              <input type="tel" formControlName="partyPhone" placeholder="01XXXXXXXXX"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
            <div class="space-y-1">
              <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">Due date</label>
              <input type="date" formControlName="dueDate"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              <p class="text-[10px] text-gray-500 m-0">Leave empty for 30 days.</p>
            </div>
            <div class="space-y-1">
              <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">
                {{ kind === 'Payable' ? 'Paid now (advance)' : 'Received now (advance)' }}
              </label>
              <div class="flex gap-2">
                <input type="number" formControlName="paidNowBdt" min="0" step="1"
                  class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                <select formControlName="paymentMethod"
                  class="px-2 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-xs bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                  @for (m of methods; track m) { <option [value]="m">{{ m }}</option> }
                </select>
              </div>
              @if (total && balance !== null) {
                <p class="text-[10px] m-0" [ngClass]="balance <= 0 ? 'text-rose-600' : 'text-gray-500'">
                  {{ balance <= 0 ? 'Paid in full — choose “' + (kind === 'Payable' ? 'Paid' : 'Received') + ' in full now” instead.' : 'Balance on credit: ৳ ' + (balance | number: '1.0-2') }}
                </p>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class CreditTermsFieldsComponent {
  @Input({ required: true }) group!: FormGroup;
  /** Payable: the farm buys on credit. Receivable: the farm sells on credit. */
  @Input() kind: 'Payable' | 'Receivable' = 'Payable';
  /** Deal total, to show the balance left on credit. */
  @Input() total: number | null = null;
  @Input() requireParty = true;
  @Input() partyPlaceholder = 'Name';

  readonly methods = PAYMENT_METHODS;

  get onCredit(): boolean {
    return !!this.group?.get('onCredit')?.value;
  }

  get balance(): number | null {
    if (!this.total) return null;
    return Math.round((this.total - (Number(this.group.get('paidNowBdt')?.value) || 0)) * 100) / 100;
  }

  set(onCredit: boolean): void {
    this.group.get('onCredit')!.setValue(onCredit);
  }
}
