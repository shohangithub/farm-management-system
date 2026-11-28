import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinancialTransaction, PAYMENT_METHODS, TradeBill, categoryLabel } from '../../models/finance.model';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { PaidFromComponent, buildPaidFromGroup, toPaidFrom } from '../paid-from/paid-from.component';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export type TradeBillDialogMode = 'payment' | 'write-off' | 'terms' | 'open';

export interface TradeBillDialogData {
  mode: TradeBillDialogMode;
  /** The bill (payment, write-off, terms). */
  bill?: TradeBill;
  /** The cash-booked ledger entry to put on credit (open). */
  transaction?: FinancialTransaction;
}

/**
 * One dialog for everything done to a credit bill: record a payment / collection, write off the
 * rest, change the party or due date, or put an existing cash entry on credit.
 */
@Component({
  selector: 'app-trade-bill-dialog',
  standalone: true,
  imports: [TranslatePipe, MoneyPipe, CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, PaidFromComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px]" [ngClass]="accent">{{ icon }}</mat-icon>
            {{ title | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">{{ subtitle | t }}</p>
        </div>
        <button mat-dialog-close type="button" [attr.aria-label]="'Close' | t" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <form [formGroup]="form" (ngSubmit)="submit()" class="flex flex-col overflow-hidden">
        @if (error()) {
          <div class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm flex items-start gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <div class="p-6 space-y-4 overflow-y-auto flex-1">
          <!-- Context -->
          @if (data.bill; as b) {
            <div class="rounded-xl p-4 border"
              [ngClass]="isPayable ? 'bg-rose-50/60 dark:bg-rose-950/20 border-rose-100 dark:border-rose-900/40' : 'bg-success-50/60 dark:bg-success-950/20 border-success-100 dark:border-success-900/40'">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <div class="text-sm font-bold text-gray-900 dark:text-white">{{ b.partyName }}</div>
                  <div class="text-[11px] text-gray-500 dark:text-gray-400">{{ b.referenceNo }} · {{ b.description }}</div>
                </div>
                <div class="text-right">
                  <div class="text-[11px] uppercase tracking-wider font-bold text-gray-400">{{ 'Outstanding' | t }}</div>
                  <div class="text-base font-black" [ngClass]="isPayable ? 'text-rose-600 dark:text-rose-400' : 'text-success-700 dark:text-success-300'">
                    {{ b.outstandingBdt | money:2 }}
                  </div>
                  <div class="text-[11px] text-gray-500">{{ 'of {amount}' | t: { amount: (b.amountBdt | money:2) } }}</div>
                </div>
              </div>
            </div>
          }
          @if (data.transaction; as t) {
            <div class="rounded-xl p-4 border bg-gray-50 dark:bg-gray-800/50 border-gray-200 dark:border-gray-700">
              <div class="text-sm font-bold text-gray-900 dark:text-white">{{ label(t.category) }} · {{ t.amountBdt | money:2 }}</div>
              <div class="text-[11px] text-gray-500 dark:text-gray-400">{{ t.transactionDate | date: 'dd MMM yyyy' }} · {{ t.description }}</div>
              <p class="text-[11px] text-amber-700 dark:text-amber-300 mt-2 mb-0">
                {{ (t.type === 'Income'
                  ? 'This entry is booked as received in cash. Putting it on credit removes it from the cash book and records it as owed by the customer until settled.'
                  : 'This entry is booked as paid in cash. Putting it on credit removes it from the cash book and records it as owed to the supplier until settled.') | t }}
              </p>
            </div>
          }

          @switch (data.mode) {
            @case ('payment') {
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Amount (BDT)' | t }} <span class="text-red-500">*</span></label>
                  <input type="number" formControlName="amountBdt" min="0.01" step="0.01" [class]="input">
                  <button type="button" (click)="payAll()" class="text-[11px] px-2 py-0.5 rounded-md bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200">
                    {{ 'Full balance' | t }}
                  </button>
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Date' | t }} <span class="text-red-500">*</span></label>
                  <input type="date" formControlName="paymentDate" [max]="today" [class]="input">
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Method' | t }}</label>
                  <select formControlName="method" [class]="input">
                    @for (m of methods; track m) { <option [value]="m">{{ m | t }}</option> }
                  </select>
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Receipt / Txn No.' | t }}</label>
                  <input type="text" formControlName="reference" [placeholder]="'Optional' | t" [class]="input">
                </div>
              </div>
              <div class="space-y-1">
                <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Notes' | t }}</label>
                <input type="text" formControlName="notes" [placeholder]="'Optional' | t" [class]="input">
              </div>
              <app-paid-from [group]="paidFromGroup" [direction]="isPayable ? 'out' : 'in'" [allowCredit]="false"
                [allowPartner]="isPayable" [amount]="form.value.amountBdt"></app-paid-from>
            }
            @case ('write-off') {
              <p class="text-xs text-gray-600 dark:text-gray-300 m-0">
                {{ (isPayable
                  ? 'Use this when the supplier has agreed you need not pay the rest (a discount or waiver). It is booked as other income.'
                  : 'Use this when the customer will not pay. The amount is booked as a bad-debt expense, so profit and assets are no longer overstated.') | t }}
              </p>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Amount (BDT)' | t }}</label>
                  <input type="number" formControlName="amountBdt" min="0.01" step="0.01" [class]="input">
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Date' | t }} <span class="text-red-500">*</span></label>
                  <input type="date" formControlName="date" [class]="input">
                </div>
              </div>
              <div class="space-y-1">
                <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Reason' | t }}</label>
                <input type="text" formControlName="reason" [placeholder]="'e.g. Buyer left the area' | t" [class]="input">
              </div>
            }
            @default {
              <!-- terms & open -->
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ (isPayable ? 'Supplier' : 'Customer') | t }} <span class="text-red-500">*</span></label>
                  <input type="text" formControlName="partyName" [class]="input">
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Phone' | t }}</label>
                  <input type="tel" formControlName="partyPhone" [class]="input">
                </div>
                <div class="space-y-1">
                  <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Due date' | t }}</label>
                  <input type="date" formControlName="dueDate" [class]="input">
                </div>
                @if (data.mode === 'open') {
                  <div class="space-y-1">
                    <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ (isPayable ? 'Paid on the day' : 'Received on the day') | t }}</label>
                    <div class="flex gap-2">
                      <input type="number" formControlName="paidNowBdt" min="0" step="0.01" [class]="input">
                      <select formControlName="paymentMethod" class="px-2 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-xs bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                        @for (m of methods; track m) { <option [value]="m">{{ m | t }}</option> }
                      </select>
                    </div>
                  </div>
                }
              </div>
              <div class="space-y-1">
                <label class="block text-[11px] font-bold uppercase tracking-wider text-gray-500">{{ 'Notes' | t }}</label>
                <input type="text" formControlName="notes" [class]="input">
              </div>
              @if (data.mode === 'open' && (form.value.paidNowBdt || 0) > 0) {
                <app-paid-from [group]="paidFromGroup" [direction]="isPayable ? 'out' : 'in'" [allowCredit]="false"
                  [allowPartner]="false" [amount]="form.value.paidNowBdt"></app-paid-from>
              }
            }
          }
        </div>

        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="busy()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50">
            {{ 'Cancel' | t }}
          </button>
          <button type="submit" [disabled]="form.invalid || busy()"
            class="px-4 py-2 text-sm font-semibold text-white rounded-xl disabled:opacity-50 flex items-center gap-2"
            [ngClass]="data.mode === 'write-off' ? 'bg-amber-600 hover:bg-amber-700' : 'bg-emerald-600 hover:bg-emerald-700'">
            @if (busy()) { <mat-icon class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon> }
            <span>{{ (busy() ? 'Saving…' : action) | t }}</span>
          </button>
        </div>
      </form>
    </div>
  `,
})
export class TradeBillDialogComponent implements OnInit {
  readonly data = inject<TradeBillDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TradeBillDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);

  readonly busy = signal(false);
  readonly error = signal('');
  readonly methods = PAYMENT_METHODS;
  readonly today = toLocalIsoDate();
  readonly input = 'block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white';
  readonly label = categoryLabel;

  form!: FormGroup;

  get isPayable(): boolean {
    if (this.data.bill) return this.data.bill.kind === 'Payable';
    return this.data.transaction?.type !== 'Income';
  }

  get title(): string {
    switch (this.data.mode) {
      case 'payment': return this.isPayable ? 'Pay Supplier' : 'Collect from Customer';
      case 'write-off': return this.isPayable ? 'Supplier Waiver' : 'Write Off Bad Debt';
      case 'terms': return 'Edit Credit Terms';
      default: return 'Put on Credit';
    }
  }

  get subtitle(): string {
    switch (this.data.mode) {
      case 'payment': return this.isPayable ? 'বকেয়া পরিশোধ — cash leaves the cash book now' : 'বকেয়া আদায় — cash enters the cash book now';
      case 'write-off': return 'Clear the rest of the bill without cash';
      case 'terms': return 'Change who the bill is with and when it is due';
      default: return 'বাকিতে — not paid in full on the day';
    }
  }

  get action(): string {
    switch (this.data.mode) {
      case 'payment': return this.isPayable ? 'Record Payment' : 'Record Collection';
      case 'write-off': return 'Write Off';
      case 'terms': return 'Save Terms';
      default: return 'Put on Credit';
    }
  }

  get icon(): string {
    return { payment: 'payments', 'write-off': 'money_off', terms: 'edit_calendar', open: 'schedule' }[this.data.mode];
  }

  get accent(): string {
    return this.data.mode === 'write-off' ? 'text-amber-500' : 'text-emerald-500';
  }

  ngOnInit(): void {
    const b = this.data.bill;
    switch (this.data.mode) {
      case 'payment':
        this.form = this.fb.group({
          amountBdt: [b?.outstandingBdt ?? null, [Validators.required, Validators.min(0.01), Validators.max(b?.outstandingBdt ?? Number.MAX_SAFE_INTEGER)]],
          paymentDate: [this.today, Validators.required],
          method: ['Cash'],
          reference: [''],
          notes: [''],
          paidFrom: buildPaidFromGroup(this.fb),
        });
        break;
      case 'write-off':
        this.form = this.fb.group({
          amountBdt: [b?.outstandingBdt ?? null, [Validators.min(0.01), Validators.max(b?.outstandingBdt ?? Number.MAX_SAFE_INTEGER)]],
          date: [this.today, Validators.required],
          reason: [''],
        });
        break;
      default:
        this.form = this.fb.group({
          partyName: [b?.partyName ?? '', Validators.required],
          partyPhone: [b?.partyPhone ?? ''],
          dueDate: [b?.dueDate ?? ''],
          paidNowBdt: [0, [Validators.min(0), Validators.max(this.data.transaction?.amountBdt ?? Number.MAX_SAFE_INTEGER)]],
          paymentMethod: ['Cash'],
          notes: [b?.notes ?? ''],
          paidFrom: buildPaidFromGroup(this.fb),
        });
    }
  }

  get paidFromGroup(): FormGroup {
    return this.form.get('paidFrom') as FormGroup;
  }

  payAll(): void {
    this.form.patchValue({ amountBdt: this.data.bill?.outstandingBdt ?? 0 });
  }

  submit(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (this.form.invalid || !farmId) return;

    const v = this.form.value;
    const billId = this.data.bill?.id ?? '';
    let call: Observable<unknown>;
    switch (this.data.mode) {
      case 'payment':
      {
        const { paidFrom, ...rest } = v;
        const source = toPaidFrom(paidFrom);
        // Payable: out of an account or paid by a partner. Receivable: into an account.
        call = this.finance.recordBillPayment(farmId, billId, {
          ...rest,
          moneyAccountId: source.moneyAccountId,
          paidByPartnerId: this.isPayable ? source.paidByPartnerId : null,
        });
      }
        break;
      case 'write-off':
        call = this.finance.writeOffTradeBill(farmId, billId, { date: v.date, reason: v.reason, amountBdt: v.amountBdt || null });
        break;
      case 'terms':
        call = this.finance.updateTradeBillTerms(farmId, billId, { partyName: v.partyName, partyPhone: v.partyPhone, dueDate: v.dueDate || null, notes: v.notes });
        break;
      default:
        call = this.finance.openTradeBill(farmId, {
          transactionId: this.data.transaction!.id,
          partyName: v.partyName,
          partyPhone: v.partyPhone,
          dueDate: v.dueDate || null,
          paidNowBdt: Number(v.paidNowBdt) || 0,
          paymentMethod: v.paymentMethod,
          notes: v.notes,
          moneyAccountId: toPaidFrom(v.paidFrom).moneyAccountId,
        });
    }

    this.busy.set(true);
    this.error.set('');
    call.subscribe({
      next: result => {
        this.busy.set(false);
        this.dialogRef.close(result ?? true);
      },
      error: err => {
        this.busy.set(false);
        this.error.set(parseApiError(err));
      },
    });
  }
}
