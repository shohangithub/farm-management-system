import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { Investor, MoneyAccount, RecordInvestorTransactionRequest, moneyAccountName } from '../../models/finance.model';
import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';

export type PartnerMoneyType = 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution';

export interface InvestorTransactionDialogData {
  /** The partner; when absent the dialog lets the user pick one. */
  investor?: Investor | null;
  defaultType?: PartnerMoneyType;
  /** Preselected money account (e.g. the one a form found short of money). */
  moneyAccountId?: string | null;
  /** Suggested amount. */
  amountBdt?: number | null;
}

/**
 * Partner money: a partner puts capital in (INTO a chosen money account), takes capital out, or is
 * paid profit (OUT of a chosen account). Opened from the Partners page, or from a "not enough money
 * in this account" warning on any spending form.
 */
@Component({
  selector: 'app-investor-transaction-dialog',
  standalone: true,
  imports: [TranslatePipe, MoneyPipe, CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-primary-600 dark:text-primary-400">handshake</mat-icon>
            {{ 'Partner money' | t }}
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">{{ 'Money a partner puts in, takes out, or is paid as profit — always through one of the farm\\'s accounts.' | t }}</p>
        </div>
        <button mat-dialog-close type="button" [attr.aria-label]="'Close' | t" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-5 overflow-y-auto flex-1">
          @if (!data.investor) {
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Partner' | t }} <span class="text-red-500">*</span></label>
              <select formControlName="partnerId" class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
                <option [ngValue]="null" disabled>{{ 'Choose a partner…' | t }}</option>
                @for (p of partners(); track p.id) {
                  <option [ngValue]="p.id">{{ p.name }} — {{ p.currentCapitalBdt | money }}</option>
                }
              </select>
              @if (partnersLoaded() && partners().length === 0) {
                <p class="text-[11px] text-amber-700 dark:text-amber-300 m-0">{{ 'No partners yet. Add the partners (the owner too) on the Partners page first.' | t }}</p>
              }
            </div>
          }

          @if (partner(); as p) {
            <div class="rounded-xl p-4 border border-primary-100 dark:border-primary-800/40 bg-primary-50/60 dark:bg-primary-900/20">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-lg bg-primary-600 text-white flex items-center justify-center font-bold">{{ p.name.charAt(0).toUpperCase() }}</div>
                <div class="min-w-0">
                  <p class="text-sm font-bold text-gray-900 dark:text-white m-0 truncate">{{ p.name }}</p>
                  <p class="text-xs text-primary-700 dark:text-primary-300 m-0">
                    {{ 'Capital {amount} · share {percent}%' | t: { amount: (p.currentCapitalBdt | money), percent: p.effectiveSharePercentage } }}
                  </p>
                </div>
              </div>
            </div>
          }

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'What happened?' | t }} <span class="text-red-500">*</span></label>
            <div class="grid grid-cols-3 gap-2">
              <button type="button" (click)="setType('CapitalContribution')"
                [ngClass]="selectedType() === 'CapitalContribution' ? 'bg-success-600 text-white ring-2 ring-success-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_circle</mat-icon><span>{{ 'Put money in' | t }}</span>
              </button>
              <button type="button" (click)="setType('CapitalWithdrawal')"
                [ngClass]="selectedType() === 'CapitalWithdrawal' ? 'bg-amber-600 text-white ring-2 ring-amber-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">remove_circle</mat-icon><span>{{ 'Take capital out' | t }}</span>
              </button>
              <button type="button" (click)="setType('ProfitDistribution')"
                [ngClass]="selectedType() === 'ProfitDistribution' ? 'bg-primary-600 text-white ring-2 ring-primary-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">payments</mat-icon><span>{{ 'Pay profit' | t }}</span>
              </button>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              {{ (selectedType() === 'CapitalContribution' ? 'Money goes into' : 'Paid out of') | t }} <span class="text-red-500">*</span>
            </label>
            <select formControlName="moneyAccountId" class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
              @for (a of accounts(); track a.id) {
                <option [ngValue]="a.id">{{ accountName(a.name) }} — {{ a.balanceBdt | money }}</option>
              }
            </select>
            @if (shortBy(); as s) {
              <p class="text-xs text-amber-700 dark:text-amber-300 m-0">
                {{ '{account} has {balance}; this spends {amount}.' | t: { account: accountName(s.name), balance: (s.balance | money), amount: (s.amount | money) } }}
              </p>
            }
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between items-center">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Amount (BDT)' | t }} <span class="text-red-500">*</span></label>
              @if (selectedType() === 'CapitalWithdrawal' && partner(); as p) {
                <button type="button" (click)="form.patchValue({ amountBdt: p.currentCapitalBdt })" class="text-[11px] text-amber-700 dark:text-amber-300 font-bold hover:underline">
                  {{ 'Withdraw Max ({amount})' | t: { amount: (p.currentCapitalBdt | money) } }}
                </button>
              }
            </div>
            <input type="number" formControlName="amountBdt" step="1" min="1" [placeholder]="'e.g. 50000' | t"
              class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            @if (isWithdrawalExceeded()) {
              <p class="text-xs text-red-600 dark:text-red-400 font-medium m-0">
                {{ 'Withdrawal amount exceeds available capital of {amount}.' | t: { amount: (partner()?.currentCapitalBdt | money) } }}
              </p>
            }
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Transaction Date' | t }} <span class="text-red-500">*</span></label>
              <input type="date" formControlName="transactionDate"
                class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Reference (Optional)' | t }}</label>
              <input type="text" formControlName="referenceId" [placeholder]="'e.g. BANK-TRF-001' | t"
                class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white">
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">{{ 'Notes / Description (Optional)' | t }}</label>
            <textarea formControlName="notes" rows="2"
              class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white"></textarea>
          </div>
        </div>

        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isLoading()"
            class="px-4 py-2 rounded-xl text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700">
            {{ 'Cancel' | t }}
          </button>
          <button type="submit" [disabled]="form.invalid || isLoading() || isWithdrawalExceeded() || !partner()"
            class="px-5 py-2 rounded-xl text-sm font-semibold text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="!text-[18px] !w-[18px] !h-[18px] animate-spin">sync</mat-icon>
            <span>{{ 'Record Transaction' | t }}</span>
          </button>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvestorTransactionDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<InvestorTransactionDialogComponent>);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private readonly i18n = inject(I18nService);
  readonly data: InvestorTransactionDialogData = inject(MAT_DIALOG_DATA, { optional: true }) ?? {};

  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedType = signal<PartnerMoneyType>('CapitalContribution');
  readonly partners = signal<Investor[]>([]);
  readonly partnersLoaded = signal(false);
  readonly accounts = signal<MoneyAccount[]>([]);
  private readonly partnerId = signal<string | null>(null);
  private readonly accountId = signal<string | null>(null);
  private readonly amount = signal<number>(0);
  readonly accountName = moneyAccountName;

  readonly partner = computed<Investor | null>(() =>
    this.data.investor ?? this.partners().find(p => p.id === this.partnerId()) ?? null);

  /** Money going OUT of an account that does not hold enough (a warning, not a block). */
  readonly shortBy = computed(() => {
    if (this.selectedType() === 'CapitalContribution') return null;
    const account = this.accounts().find(a => a.id === this.accountId());
    const amount = this.amount();
    return account && amount > account.balanceBdt ? { name: account.name, balance: account.balanceBdt, amount } : null;
  });

  form!: FormGroup;

  ngOnInit(): void {
    const initialType = this.data.defaultType || 'CapitalContribution';
    this.selectedType.set(initialType);

    this.form = this.fb.group({
      partnerId: [this.data.investor?.id ?? null, [Validators.required]],
      type: [initialType, [Validators.required]],
      moneyAccountId: [this.data.moneyAccountId ?? null, [Validators.required]],
      amountBdt: [this.data.amountBdt ?? null, [Validators.required, Validators.min(1)]],
      transactionDate: [toLocalIsoDate(), [Validators.required]],
      referenceId: [''],
      notes: ['']
    });
    this.partnerId.set(this.form.value.partnerId);
    this.accountId.set(this.form.value.moneyAccountId);
    this.amount.set(Number(this.form.value.amountBdt) || 0);
    this.form.valueChanges.subscribe(v => {
      this.partnerId.set(v.partnerId);
      this.accountId.set(v.moneyAccountId);
      this.amount.set(Number(v.amountBdt) || 0);
    });

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;
    this.financeService.getMoneyAccounts(farmId, false).subscribe({
      next: list => {
        this.accounts.set(list);
        if (!this.form.value.moneyAccountId) {
          this.form.patchValue({ moneyAccountId: list.find(a => a.isDefault)?.id ?? list[0]?.id ?? null });
        }
      },
      error: err => this.error.set(parseApiError(err)),
    });
    if (!this.data.investor) {
      this.financeService.getInvestors(farmId, false).subscribe({
        next: list => {
          this.partners.set(list);
          this.partnersLoaded.set(true);
          if (list.length === 1) this.form.patchValue({ partnerId: list[0].id });
        },
        error: err => { this.partnersLoaded.set(true); this.error.set(parseApiError(err)); },
      });
    }
  }

  setType(type: PartnerMoneyType): void {
    this.selectedType.set(type);
    this.form.patchValue({ type });
  }

  isWithdrawalExceeded(): boolean {
    if (this.selectedType() !== 'CapitalWithdrawal') return false;
    const p = this.partner();
    return !!p && this.amount() > p.currentCapitalBdt;
  }

  onSubmit(): void {
    const p = this.partner();
    if (this.form.invalid || this.isWithdrawalExceeded() || !p) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set(this.i18n.t('No active farm selected in context.'));
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();
    const request: RecordInvestorTransactionRequest = {
      type: this.selectedType(),
      amountBdt: Number(raw.amountBdt),
      transactionDate: raw.transactionDate,
      referenceId: raw.referenceId || null,
      notes: raw.notes || null,
      moneyAccountId: raw.moneyAccountId || null,
    };

    this.financeService.recordInvestorTransaction(farmId, p.id, request).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.dialogRef.close(result);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
