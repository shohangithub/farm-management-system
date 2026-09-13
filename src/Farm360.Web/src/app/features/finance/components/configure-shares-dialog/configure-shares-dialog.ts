import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { FarmShareConfig, ConfigureFarmSharesRequest } from '../../models/finance.model';

export interface ConfigureSharesDialogData {
  farmId: string;
  config?: FarmShareConfig | null;
}

@Component({
  selector: 'app-configure-shares-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
    CurrencyPipe,
    DecimalPipe
  ],
  template: `
    <div class="bg-white dark:bg-gray-900 rounded-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh] w-full max-w-xl">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 bg-gradient-to-r from-teal-500/10 via-emerald-500/5 to-transparent dark:from-teal-950/40 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20">
            <mat-icon class="!text-[22px] !w-[22px] !h-[22px]">tune</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">
              {{ isEditing ? 'Update Farm Share Structure' : 'Set Up Farm Share Market' }}
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              Define total shares, share price valuation, and retained owner allocation
            </p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Form -->
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="flex flex-col overflow-hidden">
        
        <!-- Error State -->
        <div *ngIf="error()" class="mx-6 mt-4 p-3 bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-300 border border-red-200 dark:border-red-800 rounded-xl text-sm whitespace-pre-wrap flex items-start gap-2">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px] text-red-500 mt-0.5 shrink-0">error</mat-icon>
          <span>{{ error() }}</span>
        </div>

        <!-- Scrollable Body -->
        <div class="p-6 space-y-4 overflow-y-auto max-h-[60vh] space-y-4">
          
          <!-- Live Valuation Card Preview -->
          <div class="p-4 rounded-xl bg-gradient-to-br from-teal-50 to-emerald-50/50 dark:from-teal-950/40 dark:to-emerald-950/20 border border-teal-200/60 dark:border-teal-800/40 grid grid-cols-2 gap-3 text-xs">
            <div>
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Estimated Total Valuation</span>
              <span class="text-base font-extrabold text-teal-700 dark:text-teal-300">
                {{ computedTotalValuation() | currency:'BDT':'symbol':'1.0-0' }}
              </span>
            </div>
            <div>
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Available Pool Valuation</span>
              <span class="text-base font-extrabold text-emerald-700 dark:text-emerald-300">
                {{ computedAvailableValuation() | currency:'BDT':'symbol':'1.0-0' }}
              </span>
            </div>
            <div class="border-t border-teal-200/40 dark:border-teal-800/40 pt-2">
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Available Shares</span>
              <span class="text-sm font-bold text-gray-800 dark:text-gray-200">
                {{ computedAvailableShares() | number }} shares
              </span>
            </div>
            <div class="border-t border-teal-200/40 dark:border-teal-800/40 pt-2">
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Owner Ownership</span>
              <span class="text-sm font-bold text-gray-800 dark:text-gray-200">
                {{ computedOwnerPercentage() | number:'1.1-2' }}%
              </span>
            </div>
          </div>

          <!-- Total Authorized Shares & Share Price -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Total Shares <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="totalShares" placeholder="e.g. 1000" min="1" step="1"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Price Per Share (BDT) <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="sharePriceBdt" placeholder="e.g. 5000" min="1" step="0.01"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
            </div>
          </div>

          <!-- Owner Retained Shares & Minimum Purchase -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Owner Retained Shares <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="ownerShareCount" placeholder="e.g. 600" min="0" step="1"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
              <span class="text-[11px] text-gray-500 dark:text-gray-400">Shares held exclusively by you</span>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Minimum Purchase (Shares) <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="minimumPurchaseShares" placeholder="e.g. 1" min="1" step="1"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow">
              <span class="text-[11px] text-gray-500 dark:text-gray-400">Min shares per transaction</span>
            </div>
          </div>

          <!-- Share Sale Open Switch -->
          <div class="flex items-center justify-between p-3.5 rounded-xl bg-gray-50 dark:bg-gray-800/60 border border-gray-200 dark:border-gray-700">
            <div>
              <span class="text-xs font-bold text-gray-800 dark:text-gray-200 block">Open for Share Purchases</span>
              <span class="text-[11px] text-gray-500 dark:text-gray-400">Allow investors to purchase available shares</span>
            </div>
            <label class="relative inline-flex items-center cursor-pointer">
              <input type="checkbox" formControlName="isShareSaleOpen" class="sr-only peer">
              <div class="w-11 h-6 bg-gray-300 peer-focus:outline-none rounded-full peer dark:bg-gray-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-teal-600"></div>
            </label>
          </div>

          <!-- Valuation Notes -->
          <div class="space-y-1.5">
            <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
              Valuation Notes / Basis
            </label>
            <textarea formControlName="valuationNotes" rows="2" placeholder="e.g. Valuation based on Q3 livestock inventory, land assets, and milk production yield."
                      class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 transition-shadow resize-none"></textarea>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center justify-end gap-3 shrink-0">
          <button mat-dialog-close type="button"
                  class="px-4 py-2 rounded-xl text-sm font-semibold text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
            Cancel
          </button>
          <button type="submit" [disabled]="form.invalid || isSubmitting()"
                  class="px-5 py-2 rounded-xl text-sm font-semibold text-white bg-gradient-to-r from-teal-600 to-emerald-600 hover:from-teal-700 hover:to-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-sm shadow-teal-500/20 flex items-center gap-2 transition-all">
            <mat-icon *ngIf="isSubmitting()" class="animate-spin !text-[18px] !w-[18px] !h-[18px]">refresh</mat-icon>
            <span>{{ isSubmitting() ? 'Saving...' : 'Save Configuration' }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class ConfigureSharesDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private financeService = inject(FinanceService);
  private dialogRef = inject(MatDialogRef<ConfigureSharesDialogComponent>);
  public data: ConfigureSharesDialogData = inject(MAT_DIALOG_DATA);

  public isSubmitting = signal(false);
  public error = signal<string | null>(null);

  public isEditing = false;
  public currentAllocated = 0;

  public form!: FormGroup;

  ngOnInit(): void {
    const c = this.data.config;
    this.isEditing = !!c;
    this.currentAllocated = c?.allocatedShareCount ?? 0;

    this.form = this.fb.group({
      totalShares: [c?.totalShares ?? 1000, [Validators.required, Validators.min(1)]],
      sharePriceBdt: [c?.sharePriceBdt ?? 5000, [Validators.required, Validators.min(1)]],
      ownerShareCount: [c?.ownerShareCount ?? 600, [Validators.required, Validators.min(0)]],
      minimumPurchaseShares: [c?.minimumPurchaseShares ?? 1, [Validators.required, Validators.min(1)]],
      isShareSaleOpen: [c?.isShareSaleOpen ?? true],
      valuationNotes: [c?.valuationNotes ?? '']
    });
  }

  computedTotalValuation(): number {
    const total = this.form?.get('totalShares')?.value || 0;
    const price = this.form?.get('sharePriceBdt')?.value || 0;
    return total * price;
  }

  computedAvailableShares(): number {
    const total = this.form?.get('totalShares')?.value || 0;
    const owner = this.form?.get('ownerShareCount')?.value || 0;
    return Math.max(0, total - owner - this.currentAllocated);
  }

  computedAvailableValuation(): number {
    const avail = this.computedAvailableShares();
    const price = this.form?.get('sharePriceBdt')?.value || 0;
    return avail * price;
  }

  computedOwnerPercentage(): number {
    const total = this.form?.get('totalShares')?.value || 0;
    const owner = this.form?.get('ownerShareCount')?.value || 0;
    return total > 0 ? (owner / total) * 100 : 0;
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const val = this.form.value;
    const req: ConfigureFarmSharesRequest = {
      totalShares: Number(val.totalShares),
      sharePriceBdt: Number(val.sharePriceBdt),
      ownerShareCount: Number(val.ownerShareCount),
      minimumPurchaseShares: Number(val.minimumPurchaseShares),
      isShareSaleOpen: !!val.isShareSaleOpen,
      valuationNotes: val.valuationNotes ? String(val.valuationNotes).trim() : null
    };

    this.financeService.configureShares(this.data.farmId, req).subscribe({
      next: (config) => {
        this.isSubmitting.set(false);
        this.dialogRef.close(config);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.error.set(parseApiError(err));
      }
    });
  }
}
