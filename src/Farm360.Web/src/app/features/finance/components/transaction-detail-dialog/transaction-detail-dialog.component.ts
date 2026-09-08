import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FinancialTransaction } from '../../models/finance.model';

@Component({
  selector: 'app-transaction-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule,
    CurrencyPipe,
    DatePipe
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full max-w-lg">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl flex items-center justify-center text-white shadow-md"
               [ngClass]="data.type === 'Income' ? 'bg-gradient-to-br from-emerald-500 to-teal-600 shadow-emerald-500/20' : 'bg-gradient-to-br from-rose-500 to-red-600 shadow-rose-500/20'">
            <mat-icon>{{ data.type === 'Income' ? 'arrow_downward' : 'arrow_upward' }}</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">Transaction Details</h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">Ref: {{ data.referenceId || 'N/A' }}</p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Content -->
      <div class="p-6 space-y-4 overflow-y-auto">
        <!-- Amount Banner -->
        <div class="p-4 rounded-xl border text-center"
             [ngClass]="data.type === 'Income' ? 'bg-emerald-50/50 border-emerald-100 dark:bg-emerald-950/20 dark:border-emerald-800/30' : 'bg-rose-50/50 border-rose-100 dark:bg-rose-950/20 dark:border-rose-800/30'">
          <span class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Amount</span>
          <h3 class="text-3xl font-extrabold mt-1"
              [ngClass]="data.type === 'Income' ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
            {{ data.type === 'Income' ? '+' : '-' }}{{ data.amountBdt | currency:'BDT ':'symbol':'1.2-2' }}
          </h3>
          <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold mt-2"
                [ngClass]="data.type === 'Income' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-300'">
            {{ data.type }} • {{ data.category }}
          </span>
        </div>

        <!-- Meta Grid -->
        <div class="grid grid-cols-2 gap-3 text-sm">
          <div class="p-3 bg-gray-50 dark:bg-gray-800/50 rounded-xl border border-gray-100 dark:border-gray-800">
            <span class="text-xs font-medium text-gray-400 uppercase">Transaction Date</span>
            <p class="font-semibold text-gray-800 dark:text-gray-200 mt-0.5 mb-0">{{ data.transactionDate | date:'mediumDate' }}</p>
          </div>
          <div class="p-3 bg-gray-50 dark:bg-gray-800/50 rounded-xl border border-gray-100 dark:border-gray-800">
            <span class="text-xs font-medium text-gray-400 uppercase">Logged On</span>
            <p class="font-semibold text-gray-800 dark:text-gray-200 mt-0.5 mb-0">{{ data.createdAtUtc | date:'short' }}</p>
          </div>
        </div>

        <!-- Description -->
        <div *ngIf="data.description" class="space-y-1">
          <span class="text-xs font-bold uppercase tracking-wider text-gray-500">Description</span>
          <p class="text-sm text-gray-800 dark:text-gray-200 bg-gray-50 dark:bg-gray-800/40 p-3 rounded-xl border border-gray-100 dark:border-gray-800 m-0">
            {{ data.description }}
          </p>
        </div>

        <!-- Notes -->
        <div *ngIf="data.notes" class="space-y-1">
          <span class="text-xs font-bold uppercase tracking-wider text-gray-500">Notes</span>
          <p class="text-sm text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-800/40 p-3 rounded-xl border border-gray-100 dark:border-gray-800 m-0">
            {{ data.notes }}
          </p>
        </div>

        <!-- Linked Entities -->
        <div *ngIf="data.animalId || data.batchId || data.shedId" class="pt-2 border-t border-gray-100 dark:border-gray-800">
          <span class="text-xs font-bold uppercase tracking-wider text-gray-500 block mb-2">Entity Associations</span>
          <div class="flex flex-wrap gap-2 text-xs">
            <span *ngIf="data.animalId" class="px-2.5 py-1 bg-purple-50 dark:bg-purple-900/20 text-purple-700 dark:text-purple-300 rounded-lg border border-purple-100 dark:border-purple-800 flex items-center gap-1">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">pets</mat-icon> Animal ID: {{ data.animalId.substring(0, 8) }}...
            </span>
            <span *ngIf="data.batchId" class="px-2.5 py-1 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 rounded-lg border border-blue-100 dark:border-blue-800 flex items-center gap-1">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">group_work</mat-icon> Batch ID: {{ data.batchId.substring(0, 8) }}...
            </span>
            <span *ngIf="data.shedId" class="px-2.5 py-1 bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300 rounded-lg border border-amber-100 dark:border-amber-800 flex items-center gap-1">
              <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">warehouse</mat-icon> Shed ID: {{ data.shedId.substring(0, 8) }}...
            </span>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="px-6 py-3 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex justify-end">
        <button mat-flat-button mat-dialog-close class="!rounded-xl !px-5 !py-2 !bg-gray-200 dark:!bg-gray-700 !text-gray-800 dark:!text-gray-200">
          Close
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransactionDetailDialogComponent {
  data: FinancialTransaction = inject(MAT_DIALOG_DATA);
}
