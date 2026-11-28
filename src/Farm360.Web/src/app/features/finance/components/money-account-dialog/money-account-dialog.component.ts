import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { MONEY_ACCOUNT_KINDS, MoneyAccount } from '../../models/finance.model';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/** Opens (or edits) a money account: cash, a bank account or a mobile wallet. */
@Component({
  selector: 'app-money-account-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
        <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
          <mat-icon class="text-primary-600 dark:text-primary-400">account_balance_wallet</mat-icon>
          {{ (account ? 'Edit money account' : 'New money account') | t }}
        </h2>
        <button mat-dialog-close type="button" [attr.aria-label]="'Close' | t" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full">
          <mat-icon>close</mat-icon>
        </button>
      </div>
      <form [formGroup]="form" (ngSubmit)="save()" class="p-6 space-y-4 overflow-y-auto">
        @if (error()) {
          <div class="p-3 rounded-lg text-sm bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800">{{ error() }}</div>
        }
        <div class="space-y-1">
          <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Kind' | t }}</label>
          <div class="grid grid-cols-3 gap-2">
            @for (k of kinds; track k.value) {
              <button type="button" (click)="form.patchValue({ kind: k.value })"
                class="py-2 px-2 rounded-xl text-xs font-bold flex flex-col items-center gap-1 border transition-colors"
                [ngClass]="form.value.kind === k.value
                  ? 'border-primary-500 bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-200'
                  : 'border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800'">
                <mat-icon class="!w-5 !h-5 !text-[20px]">{{ k.icon }}</mat-icon>{{ k.label | t }}
              </button>
            }
          </div>
        </div>
        <div class="space-y-1">
          <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Account name' | t }} <span class="text-red-500">*</span></label>
          <input type="text" formControlName="name" maxlength="100" [placeholder]="'e.g. Sonali Bank, bKash (owner), Cash box' | t"
            class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
        </div>
        @if (form.value.kind !== 'Cash') {
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ (form.value.kind === 'Bank' ? 'Bank name' : 'Wallet provider') | t }}</label>
              <input type="text" formControlName="providerName" maxlength="100" [placeholder]="(form.value.kind === 'Bank' ? 'e.g. Sonali Bank' : 'e.g. bKash') | t"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
            <div class="space-y-1">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ (form.value.kind === 'Bank' ? 'Account number' : 'Wallet number') | t }}</label>
              <input type="text" formControlName="accountNumber" maxlength="50"
                class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
            @if (form.value.kind === 'Bank') {
              <div class="space-y-1 sm:col-span-2">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Branch' | t }}</label>
                <input type="text" formControlName="branch" maxlength="100"
                  class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              </div>
            }
          </div>
        }
        <div class="space-y-1">
          <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Notes (Optional)' | t }}</label>
          <input type="text" formControlName="notes" maxlength="500"
            class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
        </div>
        @if (!account) {
          <p class="text-[11px] text-gray-500 dark:text-gray-400 m-0">{{ 'A new account starts at ৳0. Put money in with "Add partner money" or move it from another account with a transfer.' | t }}</p>
        }
        <div class="flex justify-end gap-2 pt-2">
          <button type="button" mat-dialog-close class="px-4 py-2 text-sm font-semibold rounded-xl border border-gray-300 dark:border-gray-700 text-gray-700 dark:text-gray-200 bg-white dark:bg-gray-800">{{ 'Cancel' | t }}</button>
          <button type="submit" [disabled]="form.invalid || saving()" class="px-4 py-2 text-sm font-semibold rounded-xl text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50">
            {{ (saving() ? 'Saving...' : 'Save') | t }}
          </button>
        </div>
      </form>
    </div>
  `,
})
export class MoneyAccountDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly finance = inject(FinanceService);
  private readonly context = inject(WorkingContextService);
  private readonly dialogRef = inject(MatDialogRef<MoneyAccountDialogComponent>);
  readonly account: MoneyAccount | null = inject(MAT_DIALOG_DATA, { optional: true })?.account ?? null;

  readonly kinds = MONEY_ACCOUNT_KINDS;
  readonly saving = signal(false);
  readonly error = signal('');

  readonly form = this.fb.group({
    kind: [this.account?.kind ?? 'Bank', Validators.required],
    name: [this.account?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    providerName: [this.account?.providerName ?? ''],
    accountNumber: [this.account?.accountNumber ?? ''],
    branch: [this.account?.branch ?? ''],
    notes: [this.account?.notes ?? ''],
  });

  save(): void {
    const farmId = this.context.currentFarmValue?.id;
    if (!farmId || this.form.invalid) return;
    const v = this.form.getRawValue();
    const request = {
      kind: v.kind!,
      name: v.name!.trim(),
      providerName: v.kind === 'Cash' ? null : v.providerName || null,
      accountNumber: v.kind === 'Cash' ? null : v.accountNumber || null,
      branch: v.kind === 'Bank' ? v.branch || null : null,
      notes: v.notes || null,
    };
    this.saving.set(true);
    this.error.set('');
    const call = this.account
      ? this.finance.updateMoneyAccount(farmId, this.account.id, request)
      : this.finance.createMoneyAccount(farmId, request);
    call.subscribe({
      next: result => { this.saving.set(false); this.dialogRef.close(result); },
      error: err => { this.saving.set(false); this.error.set(parseApiError(err)); },
    });
  }
}
