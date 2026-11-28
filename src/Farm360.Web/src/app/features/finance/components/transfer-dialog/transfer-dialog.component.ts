import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { toSignal } from '@angular/core/rxjs-interop';
import { FinanceService } from '../../services/finance.service';
import { MoneyAccount, moneyAccountName } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface TransferDialogData {
  fromAccountId?: string | null;
  toAccountId?: string | null;
  amountBdt?: number | null;
}

/**
 * Moves money between two of the farm's own accounts (e.g. bank → cash in hand). Not income or
 * expense: each account's balance changes, the farm's total cash does not.
 */
@Component({
  selector: 'app-transfer-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, MoneyPipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="text-primary-600 dark:text-primary-400">swap_horiz</mat-icon>
            {{ 'Transfer between accounts' | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">{{ 'Moves money between your own accounts. It is not income or expense; total cash stays the same.' | t }}</p>
        </div>
        <button mat-dialog-close type="button" [attr.aria-label]="'Close' | t" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <form [formGroup]="form" (ngSubmit)="save()" class="p-6 space-y-4 overflow-y-auto">
        @if (error()) {
          <div class="p-3 rounded-lg text-sm bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800">{{ error() }}</div>
        }
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="space-y-1">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'From account' | t }} <span class="text-red-500">*</span></label>
            <select formControlName="fromAccountId" class="field">
              <option [ngValue]="null" disabled>{{ 'Choose…' | t }}</option>
              @for (a of accounts(); track a.id) {
                <option [ngValue]="a.id">{{ name(a.name) }} — {{ a.balanceBdt | money }}</option>
              }
            </select>
          </div>
          <div class="space-y-1">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'To account' | t }} <span class="text-red-500">*</span></label>
            <select formControlName="toAccountId" class="field">
              <option [ngValue]="null" disabled>{{ 'Choose…' | t }}</option>
              @for (a of accounts(); track a.id) {
                <option [ngValue]="a.id" [disabled]="a.id === form.value.fromAccountId">{{ name(a.name) }} — {{ a.balanceBdt | money }}</option>
              }
            </select>
          </div>
          <div class="space-y-1">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Amount (BDT)' | t }} <span class="text-red-500">*</span></label>
            <input type="number" min="1" step="1" formControlName="amountBdt" class="field">
          </div>
          <div class="space-y-1">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Date' | t }} <span class="text-red-500">*</span></label>
            <input type="date" formControlName="transferDate" class="field">
          </div>
        </div>
        @if (short(); as s) {
          <div class="p-3 rounded-lg text-xs bg-amber-50 dark:bg-amber-950/30 text-amber-800 dark:text-amber-200 border border-amber-200 dark:border-amber-800/60">
            {{ '{account} has {balance}; this moves {amount}.' | t: { account: name(s.name), balance: (s.balance | money), amount: (s.amount | money) } }}
          </div>
        }
        <div class="space-y-1">
          <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Reference / Notes (Optional)' | t }}</label>
          <input type="text" formControlName="notes" [placeholder]="'e.g. Withdrew from bank for wages' | t" class="field">
        </div>
        <div class="flex justify-end gap-2 pt-2">
          <button type="button" mat-dialog-close class="px-4 py-2 text-sm font-semibold rounded-xl border border-gray-300 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800">{{ 'Cancel' | t }}</button>
          <button type="submit" [disabled]="form.invalid || saving() || form.value.fromAccountId === form.value.toAccountId"
            class="px-4 py-2 text-sm font-semibold rounded-xl text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 flex items-center gap-2">
            <mat-icon class="!w-4 !h-4 !text-[16px]">swap_horiz</mat-icon>{{ (saving() ? 'Saving...' : 'Transfer') | t }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`.field { display:block; width:100%; padding:.5rem .75rem; border-radius:.5rem; font-size:.875rem; border:1px solid rgb(209 213 219); background:#fff; color:#111827; }
    :host-context(.dark) .field { background: rgb(31 41 55); border-color: rgb(75 85 99); color: #fff; }`],
})
export class TransferDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialogRef = inject(MatDialogRef<TransferDialogComponent>);
  readonly data: TransferDialogData = inject(MAT_DIALOG_DATA, { optional: true }) ?? {};

  readonly accounts = signal<MoneyAccount[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly name = moneyAccountName;

  readonly form = this.fb.group({
    fromAccountId: [this.data.fromAccountId ?? null as string | null, Validators.required],
    toAccountId: [this.data.toAccountId ?? null as string | null, Validators.required],
    amountBdt: [this.data.amountBdt ?? null as number | null, [Validators.required, Validators.min(0.01)]],
    transferDate: [toLocalIsoDate(), Validators.required],
    notes: [''],
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  readonly short = computed(() => {
    const v = this.value();
    const from = this.accounts().find(a => a.id === v.fromAccountId);
    const amount = Number(v.amountBdt) || 0;
    return from && amount > from.balanceBdt ? { name: from.name, balance: from.balanceBdt, amount } : null;
  });

  ngOnInit(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId) return;
    this.finance.getMoneyAccounts(farmId, false).subscribe({
      next: list => {
        this.accounts.set(list);
        // Pick a sensible "from" when only "to" was given: the account with the most money.
        if (!this.form.value.fromAccountId) {
          const best = [...list].filter(a => a.id !== this.form.value.toAccountId).sort((a, b) => b.balanceBdt - a.balanceBdt)[0];
          if (best) this.form.patchValue({ fromAccountId: best.id });
        }
      },
      error: err => this.error.set(parseApiError(err)),
    });
  }

  save(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || this.form.invalid) return;
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set('');
    this.finance.transferBetweenAccounts(farmId, {
      fromAccountId: v.fromAccountId!,
      toAccountId: v.toAccountId!,
      amountBdt: Number(v.amountBdt),
      transferDate: v.transferDate!,
      notes: v.notes || null,
    }).subscribe({
      next: result => { this.saving.set(false); this.dialogRef.close(result); },
      error: err => { this.saving.set(false); this.error.set(parseApiError(err)); },
    });
  }
}
