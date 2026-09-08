import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FinanceService } from '../../services/finance.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FinancialTransaction, TRANSACTION_CATEGORIES } from '../../models/finance.model';

@Component({
  selector: 'app-edit-transaction-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full max-w-lg">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-2">
          <div class="w-9 h-9 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center">
            <mat-icon>edit</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">Edit Transaction</h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Update ledger record</p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error Banner -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-xl text-sm flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
          <!-- Category -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Category <span class="text-red-500">*</span></label>
            <select formControlName="category" class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 outline-none">
              <option *ngFor="let cat of availableCategories" [value]="cat">{{ cat }}</option>
            </select>
          </div>

          <!-- Amount -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Amount (BDT) <span class="text-red-500">*</span></label>
            <input type="number" formControlName="amountBdt" step="0.01" min="0.01"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 outline-none">
          </div>

          <!-- Date -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Transaction Date <span class="text-red-500">*</span></label>
            <input type="date" formControlName="transactionDate"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 outline-none">
          </div>

          <!-- Description -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Description</label>
            <input type="text" formControlName="description" placeholder="Brief note"
                   class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 outline-none">
          </div>

          <!-- Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">Notes</label>
            <textarea formControlName="notes" rows="2" placeholder="Additional details..."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 outline-none resize-none"></textarea>
          </div>
        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex justify-end gap-3 shrink-0">
          <button mat-button mat-dialog-close type="button" class="!rounded-xl !px-4 text-gray-600 dark:text-gray-400">
            Cancel
          </button>
          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="!rounded-xl !px-6 !py-2 !bg-primary-600 hover:!bg-primary-700 !text-white flex items-center gap-2">
            <span *ngIf="isSubmitting()" class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
            <span>Save Changes</span>
          </button>
        </div>
      </form>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditTransactionDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<EditTransactionDialogComponent>);
  private financeService = inject(FinanceService);

  readonly data: FinancialTransaction = inject(MAT_DIALOG_DATA);
  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);

  form!: FormGroup;
  availableCategories: string[] = [];

  ngOnInit(): void {
    const isIncome = this.data.type === 'Income';
    this.availableCategories = isIncome ? TRANSACTION_CATEGORIES.Income : TRANSACTION_CATEGORIES.Expense;

    const formattedDate = this.data.transactionDate ? this.data.transactionDate.substring(0, 10) : '';

    this.form = this.fb.group({
      category: [this.data.category, Validators.required],
      amountBdt: [this.data.amountBdt, [Validators.required, Validators.min(0.01)]],
      transactionDate: [formattedDate, Validators.required],
      description: [this.data.description || ''],
      notes: [this.data.notes || '']
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const val = this.form.value;
    const request = {
      category: val.category,
      amountBdt: Number(val.amountBdt),
      transactionDate: new Date(val.transactionDate).toISOString(),
      description: val.description,
      notes: val.notes,
      animalId: this.data.animalId,
      batchId: this.data.batchId,
      shedId: this.data.shedId
    };

    this.financeService.updateTransaction(this.data.farmId, this.data.id, request).subscribe({
      next: (res) => {
        this.dialogRef.close(res);
      },
      error: (err) => {
        this.error.set(parseApiError(err));
        this.isSubmitting.set(false);
      }
    });
  }
}
