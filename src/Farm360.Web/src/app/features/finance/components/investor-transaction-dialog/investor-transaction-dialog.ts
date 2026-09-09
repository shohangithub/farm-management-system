import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { Investor, RecordInvestorTransactionRequest } from '../../models/finance.model';

export interface InvestorTransactionDialogData {
  investor: Investor;
  defaultType?: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution';
}

@Component({
  selector: 'app-investor-transaction-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    CurrencyPipe
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-teal-600 dark:text-teal-400">price_change</mat-icon>
            Record Investor Transaction
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Record capital contribution, withdrawal, or profit distribution</p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <!-- Scrollable Body -->
        <div class="p-6 space-y-5 overflow-y-auto custom-scrollbar flex-1">
          
          <!-- Investor Snapshot Card -->
          <div class="bg-gradient-to-br from-teal-50 to-emerald-50 dark:from-teal-900/20 dark:to-emerald-900/20 rounded-xl p-4 border border-teal-100 dark:border-teal-800/30">
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 rounded-lg bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center font-bold text-base shadow-sm">
                {{ data.investor.name.charAt(0).toUpperCase() }}
              </div>
              <div>
                <p class="text-sm font-bold text-gray-900 dark:text-white m-0">{{ data.investor.name }}</p>
                <p class="text-xs text-teal-700 dark:text-teal-300 m-0">
                  Share: {{ data.investor.effectiveSharePercentage }}% 
                  <span *ngIf="data.investor.agreedProfitSharePercentage" class="text-[10px] text-gray-500">(agreed)</span>
                  <span *ngIf="!data.investor.agreedProfitSharePercentage" class="text-[10px] text-gray-500">(capital proportional)</span>
                </p>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div class="bg-white/70 dark:bg-gray-800/70 rounded-lg p-2.5 text-center">
                <p class="text-[10px] font-bold uppercase tracking-wider text-gray-400 m-0">Current Capital</p>
                <p class="text-sm font-bold text-emerald-600 dark:text-emerald-400 m-0 mt-0.5">
                  {{ data.investor.currentCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </p>
              </div>
              <div class="bg-white/70 dark:bg-gray-800/70 rounded-lg p-2.5 text-center">
                <p class="text-[10px] font-bold uppercase tracking-wider text-gray-400 m-0">Total Profit Paid</p>
                <p class="text-sm font-bold text-indigo-600 dark:text-indigo-400 m-0 mt-0.5">
                  {{ data.investor.totalProfitPaidBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </p>
              </div>
            </div>
          </div>

          <!-- Transaction Type Selector -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Transaction Action <span class="text-red-500">*</span>
            </label>
            <div class="grid grid-cols-3 gap-2">
              <button type="button" (click)="setType('CapitalContribution')"
                      [ngClass]="selectedType() === 'CapitalContribution' ? 'bg-emerald-600 text-white shadow-md shadow-emerald-500/20 ring-2 ring-emerald-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                      class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">add_circle</mat-icon>
                <span>Add Capital</span>
              </button>

              <button type="button" (click)="setType('CapitalWithdrawal')"
                      [ngClass]="selectedType() === 'CapitalWithdrawal' ? 'bg-amber-600 text-white shadow-md shadow-amber-500/20 ring-2 ring-amber-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                      class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">remove_circle</mat-icon>
                <span>Withdraw Capital</span>
              </button>

              <button type="button" (click)="setType('ProfitDistribution')"
                      [ngClass]="selectedType() === 'ProfitDistribution' ? 'bg-indigo-600 text-white shadow-md shadow-indigo-500/20 ring-2 ring-indigo-500' : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'"
                      class="py-2.5 px-2 rounded-xl text-xs font-bold transition-all flex flex-col items-center gap-1">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">payments</mat-icon>
                <span>Pay Profit</span>
              </button>
            </div>
          </div>

          <!-- Amount -->
          <div class="space-y-1.5">
            <div class="flex justify-between items-center">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                Amount (BDT) <span class="text-red-500">*</span>
              </label>
              <button *ngIf="selectedType() === 'CapitalWithdrawal' && data.investor.currentCapitalBdt > 0"
                      type="button" (click)="setFullCapital()"
                      class="text-[11px] text-amber-600 dark:text-amber-400 font-bold hover:underline">
                Withdraw Max ({{ data.investor.currentCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }})
              </button>
            </div>
            <input type="number" formControlName="amountBdt" step="100" min="1" placeholder="e.g. 50000"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            
            <p *ngIf="selectedType() === 'CapitalWithdrawal' && form.get('amountBdt')?.value > data.investor.currentCapitalBdt"
               class="text-xs text-red-500 font-medium m-0">
              Withdrawal amount exceeds available capital of {{ data.investor.currentCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}.
            </p>
          </div>

          <!-- Transaction Date -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Transaction Date <span class="text-red-500">*</span>
            </label>
            <input type="date" formControlName="transactionDate"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
          </div>

          <!-- Reference / Receipt No. -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Reference / Receipt / Voucher No. (Optional)
            </label>
            <input type="text" formControlName="referenceId" placeholder="e.g. BANK-TRF-001 or VOUCHER-45"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
          </div>

          <!-- Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Notes / Description (Optional)
            </label>
            <textarea formControlName="notes" rows="2" placeholder="e.g. Q3 Profit dividend disbursement via bank transfer"
                      class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow"></textarea>
          </div>

        </div>

        <!-- Footer Actions -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-end gap-3 shrink-0">
          <button mat-dialog-close type="button"
                  class="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm font-semibold text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
            Cancel
          </button>
          
          <button type="submit" [disabled]="form.invalid || isLoading() || isWithdrawalExceeded()"
                  class="px-5 py-2 rounded-xl text-sm font-semibold text-white bg-gradient-to-r from-teal-500 to-emerald-600 hover:from-teal-600 hover:to-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-sm shadow-teal-500/20 flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="!text-[18px] !w-[18px] !h-[18px] animate-spin">sync</mat-icon>
            <span>Record Transaction</span>
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
  readonly data: InvestorTransactionDialogData = inject(MAT_DIALOG_DATA);

  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedType = signal<'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution'>('CapitalContribution');

  form!: FormGroup;

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];
    const initialType = this.data.defaultType || 'CapitalContribution';
    this.selectedType.set(initialType);

    this.form = this.fb.group({
      type: [initialType, [Validators.required]],
      amountBdt: [null, [Validators.required, Validators.min(1)]],
      transactionDate: [today, [Validators.required]],
      referenceId: [''],
      notes: ['']
    });
  }

  setType(type: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution'): void {
    this.selectedType.set(type);
    this.form.patchValue({ type });
  }

  setFullCapital(): void {
    this.form.patchValue({ amountBdt: this.data.investor.currentCapitalBdt });
  }

  isWithdrawalExceeded(): boolean {
    if (this.selectedType() !== 'CapitalWithdrawal') return false;
    const amount = this.form.get('amountBdt')?.value;
    return amount > this.data.investor.currentCapitalBdt;
  }

  onSubmit(): void {
    if (this.form.invalid || this.isWithdrawalExceeded()) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm selected in context.');
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();
    const request: RecordInvestorTransactionRequest = {
      type: this.selectedType(),
      amountBdt: Number(raw.amountBdt),
      transactionDate: new Date(raw.transactionDate).toISOString(),
      referenceId: raw.referenceId || null,
      notes: raw.notes || null
    };

    this.financeService.recordInvestorTransaction(farmId, this.data.investor.id, request).subscribe({
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
