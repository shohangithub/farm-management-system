import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { OverheadAllocationResult } from '../../models/finance.model';

/**
 * Runs the labour/overhead allocation for a period: splits every unattributed indirect expense
 * (labour, utilities, transport, misc...) across the animals present during that window and
 * refreshes their cost ledgers. The arithmetic already exists server-side (docs/32 GAP-2) -- this
 * is the missing trigger, since until now the only way to run it was a direct API call.
 */
@Component({
  selector: 'app-allocate-overhead-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatIconModule, CurrencyPipe],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-between shrink-0">
        <div>
          <h2 class="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-2 m-0">
            <mat-icon class="!text-[20px] !w-[20px] !h-[20px] text-amber-500">engineering</mat-icon>
            Allocate Labor &amp; Overhead
          </h2>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
            Spread this period's labour and overhead expenses across the animals present during it
          </p>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      @if (!result()) {
        <!-- Form -->
        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">

          <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-lg text-sm whitespace-pre-wrap flex items-start gap-2">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
            <span>{{ error() }}</span>
          </div>

          <div class="p-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">

            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                  From <span class="text-red-500">*</span>
                </label>
                <input type="date" formControlName="from"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-500">
                  To <span class="text-red-500">*</span>
                </label>
                <input type="date" formControlName="to"
                       class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-primary-500 focus:border-primary-500 transition-shadow">
              </div>
            </div>

            <label class="flex items-start gap-2.5 cursor-pointer select-none">
              <input type="checkbox" formControlName="isBackfill" class="mt-0.5 rounded border-gray-300 text-primary-600 focus:ring-primary-500">
              <span class="text-sm text-gray-700 dark:text-gray-300">
                This is a historical backfill
                <span class="block text-xs text-gray-500 dark:text-gray-400">Just flags the rows produced -- it doesn't change the amounts</span>
              </span>
            </label>

            <div class="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-3 text-xs text-blue-800 dark:text-blue-300 flex gap-2">
              <mat-icon class="!text-[16px] !w-[16px] !h-[16px] shrink-0 mt-0.5">info</mat-icon>
              <span>
                Only expenses posted as Labor Cost, Utilities, Transport, or Miscellaneous that aren't
                already linked to one animal are picked up. Feed and veterinary costs are allocated
                separately and are not affected. Re-running a period is safe -- already-allocated
                transactions are skipped.
              </span>
            </div>

          </div>

          <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end gap-3 shrink-0">
            <button type="button" mat-dialog-close [disabled]="isLoading()"
              class="px-4 py-2 text-sm font-semibold text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl hover:bg-gray-50 transition-colors shadow-sm">
              Cancel
            </button>
            <button type="submit" [disabled]="form.invalid || isLoading()"
                    class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
              <mat-icon *ngIf="isLoading()" class="animate-spin !w-[18px] !h-[18px] !text-[18px]">autorenew</mat-icon>
              <span>{{ isLoading() ? 'Allocating...' : 'Run Allocation' }}</span>
            </button>
          </div>
        </form>
      } @else {
        <!-- Result summary -->
        <div class="p-6 space-y-4">
          <div class="flex items-center gap-3 p-3 bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 rounded-lg">
            <mat-icon class="!text-[22px] !w-[22px] !h-[22px] text-emerald-600 dark:text-emerald-400">check_circle</mat-icon>
            <span class="text-sm font-semibold text-emerald-800 dark:text-emerald-300">Allocation complete</span>
          </div>

          <dl class="grid grid-cols-2 gap-3 text-sm">
            <div class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <dt class="text-xs text-gray-500 dark:text-gray-400">Amount allocated</dt>
              <dd class="font-bold text-gray-900 dark:text-white m-0">{{ result()!.amountAllocatedBdt | currency:'BDT':'symbol-narrow':'1.0-2' }}</dd>
            </div>
            <div class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <dt class="text-xs text-gray-500 dark:text-gray-400">Animal ledgers updated</dt>
              <dd class="font-bold text-gray-900 dark:text-white m-0">{{ result()!.ledgersUpdated }}</dd>
            </div>
            <div class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <dt class="text-xs text-gray-500 dark:text-gray-400">Transactions allocated</dt>
              <dd class="font-bold text-gray-900 dark:text-white m-0">{{ result()!.transactionsAllocated }} / {{ result()!.transactionsConsidered }}</dd>
            </div>
            <div class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <dt class="text-xs text-gray-500 dark:text-gray-400">Skipped</dt>
              <dd class="font-bold text-gray-900 dark:text-white m-0">
                {{ result()!.transactionsSkippedAlreadyAllocated }} already done, {{ result()!.transactionsSkippedNoAnimals }} no animals
              </dd>
            </div>
          </dl>
        </div>

        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50 dark:bg-gray-800/50 flex justify-end shrink-0">
          <button type="button" (click)="dialogRef.close(result())"
                  class="px-4 py-2 text-sm font-semibold text-white bg-primary-600 rounded-xl hover:bg-primary-700 transition-colors shadow-sm shadow-primary-500/30">
            Done
          </button>
        </div>
      }
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
export class AllocateOverheadDialogComponent implements OnInit {
  protected dialogRef = inject(MatDialogRef<AllocateOverheadDialogComponent>);
  private fb = inject(FormBuilder);
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);

  form!: FormGroup;
  isLoading = signal(false);
  error = signal('');
  result = signal<OverheadAllocationResult | null>(null);

  ngOnInit(): void {
    const today = new Date();
    const firstOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);

    this.form = this.fb.group({
      from: [this.toDateString(firstOfMonth), Validators.required],
      to: [this.toDateString(today), Validators.required],
      isBackfill: [false],
    });
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

    this.financeService.allocateOverhead(farmId, this.form.value).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.result.set(result);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }

  private toDateString(date: Date): string {
    return date.toISOString().split('T')[0];
  }
}
