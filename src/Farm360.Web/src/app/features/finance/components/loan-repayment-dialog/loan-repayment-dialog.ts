import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { LoanRecord } from '../../models/finance.model';

@Component({
  selector: 'app-loan-repayment-dialog',
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
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-emerald-500">payments</mat-icon>
            Record Repayment
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Record a payment against an active loan</p>
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
          
          <!-- Loan Context Card -->
          <div class="bg-gradient-to-br from-indigo-50 to-purple-50 dark:from-indigo-900/20 dark:to-purple-900/20 rounded-xl p-4 border border-indigo-100 dark:border-indigo-800/30">
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 text-white flex items-center justify-center shadow-md shadow-indigo-500/20">
                <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">real_estate_agent</mat-icon>
              </div>
              <div>
                <p class="text-sm font-bold text-gray-900 dark:text-white m-0">{{ data.lenderName }}</p>
                <p class="text-xs text-gray-500 dark:text-gray-400 m-0">{{ data.schedule }} schedule</p>
              </div>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div class="bg-white/60 dark:bg-gray-800/60 rounded-lg p-2.5 text-center">
                <p class="text-[10px] font-bold uppercase tracking-wider text-gray-400 m-0">Principal</p>
                <p class="text-sm font-bold text-gray-900 dark:text-white m-0 mt-0.5">{{ data.principalAmountBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>
              <div class="bg-white/60 dark:bg-gray-800/60 rounded-lg p-2.5 text-center">
                <p class="text-[10px] font-bold uppercase tracking-wider text-gray-400 m-0">Outstanding</p>
                <p class="text-sm font-bold text-rose-600 dark:text-rose-400 m-0 mt-0.5">{{ data.outstandingBalanceBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>
            </div>
          </div>

          <!-- Repayment Amount -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Repayment Amount (BDT) <span class="text-red-500">*</span>
            </label>
            <input type="number" formControlName="amountBdt" step="100" min="1" placeholder="e.g. 10000"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-shadow">
            <div *ngIf="data.outstandingBalanceBdt > 0" class="flex gap-2 mt-1.5">
              <button type="button" (click)="setFullAmount()"
                class="text-xs px-2.5 py-1 rounded-lg bg-emerald-50 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800 hover:bg-emerald-100 dark:hover:bg-emerald-900/50 transition-colors font-medium">
                Pay Full ({{ data.outstandingBalanceBdt | currency:'BDT ':'symbol':'1.0-0' }})
              </button>
            </div>
          </div>

          <!-- Repayment Date -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Repayment Date <span class="text-red-500">*</span>
            </label>
            <input type="date" formControlName="repaymentDate"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-shadow">
          </div>

          <!-- Reference ID -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Reference / Receipt No. (Optional)
            </label>
            <input type="text" formControlName="referenceId" placeholder="e.g. TRX-2026-0912"
                   class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-shadow">
          </div>

          <!-- Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
              Notes (Optional)
            </label>
            <textarea formControlName="notes" rows="2" placeholder="Any additional repayment details..."
                      class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-shadow"></textarea>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
          <button type="button" mat-dialog-close [disabled]="isLoading()"
            class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isLoading()"
                  class="px-4 py-2 text-sm font-semibold text-white bg-emerald-600 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm shadow-emerald-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
            <span>{{ isLoading() ? 'Processing...' : 'Record Repayment' }}</span>
          </button>
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
export class LoanRepaymentDialogComponent implements OnInit {
  readonly data = inject<LoanRecord>(MAT_DIALOG_DATA);
  private dialogRef = inject(MatDialogRef<LoanRepaymentDialogComponent>);
  private fb = inject(FormBuilder);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);

  form!: FormGroup;
  isLoading = signal(false);
  error = signal('');

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];

    this.form = this.fb.group({
      amountBdt: [null, [Validators.required, Validators.min(1)]],
      repaymentDate: [today, Validators.required],
      referenceId: [''],
      notes: ['']
    });
  }

  setFullAmount(): void {
    this.form.patchValue({ amountBdt: this.data.outstandingBalanceBdt });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) {
      this.error.set('No active farm selected.');
      return;
    }

    this.isLoading.set(true);
    this.error.set('');

    const payload = this.form.value;

    this.financeService.recordLoanRepayment(farmId, this.data.id, payload).subscribe({
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
