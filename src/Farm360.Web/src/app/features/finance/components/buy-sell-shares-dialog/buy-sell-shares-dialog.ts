import { Component, ChangeDetectionStrategy, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { FinanceService } from '../../services/finance.service';
import { parseApiError } from '../../../../core/utils/error-parser';
import { 
  FarmShareConfig, 
  ShareHolding, 
  Investor, 
  PurchaseSharesRequest, 
  SellSharesRequest, 
  TransferSharesRequest, 
  UpdateShareValuationRequest 
} from '../../models/finance.model';

export type ShareActionType = 'purchase' | 'sell' | 'transfer' | 'valuation';

export interface BuySellSharesDialogData {
  farmId: string;
  config: FarmShareConfig;
  actionType: ShareActionType;
  investors: Investor[];
  selectedInvestorId?: string;
  shareholders?: ShareHolding[];
}

@Component({
  selector: 'app-buy-sell-shares-dialog',
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
            <mat-icon class="!text-[22px] !w-[22px] !h-[22px]">{{ getHeaderIcon() }}</mat-icon>
          </div>
          <div>
            <h2 class="text-base font-bold text-gray-900 dark:text-white m-0">
              {{ getHeaderTitle() }}
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-0">
              {{ getHeaderSubtitle() }}
            </p>
          </div>
        </div>
        <button mat-dialog-close type="button" class="p-2 -mr-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-full hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
          <mat-icon class="!text-[20px] !w-[20px] !h-[20px]">close</mat-icon>
        </button>
      </div>

      <!-- Mode Selector Tabs (if not locked to valuation) -->
      <div *ngIf="actionType() !== 'valuation'" class="px-6 pt-4 pb-1 border-b border-gray-100 dark:border-gray-800 bg-gray-50/50 dark:bg-gray-800/30 flex items-center gap-2">
        <button type="button" (click)="setActionType('purchase')"
                [class]="actionType() === 'purchase' ? 'px-3.5 py-1.5 rounded-lg text-xs font-bold bg-teal-600 text-white shadow-sm' : 'px-3.5 py-1.5 rounded-lg text-xs font-medium text-gray-600 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-700'">
          <mat-icon class="!text-[14px] !w-[14px] !h-[14px] inline -mt-0.5 mr-1">add_shopping_cart</mat-icon>
          Purchase Shares
        </button>
        <button type="button" (click)="setActionType('sell')"
                [class]="actionType() === 'sell' ? 'px-3.5 py-1.5 rounded-lg text-xs font-bold bg-rose-600 text-white shadow-sm' : 'px-3.5 py-1.5 rounded-lg text-xs font-medium text-gray-600 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-700'">
          <mat-icon class="!text-[14px] !w-[14px] !h-[14px] inline -mt-0.5 mr-1">remove_shopping_cart</mat-icon>
          Redeem / Sell Back
        </button>
        <button type="button" (click)="setActionType('transfer')"
                [class]="actionType() === 'transfer' ? 'px-3.5 py-1.5 rounded-lg text-xs font-bold bg-indigo-600 text-white shadow-sm' : 'px-3.5 py-1.5 rounded-lg text-xs font-medium text-gray-600 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-700'">
          <mat-icon class="!text-[14px] !w-[14px] !h-[14px] inline -mt-0.5 mr-1">swap_horiz</mat-icon>
          Transfer Shares
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
        <div class="p-6 space-y-4 overflow-y-auto max-h-[58vh]">

          <!-- Info Banner -->
          <div *ngIf="actionType() === 'purchase'" class="p-3.5 rounded-xl bg-teal-50 dark:bg-teal-950/40 border border-teal-200/60 dark:border-teal-800/40 flex items-center justify-between text-xs">
            <div>
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Available Pool</span>
              <span class="text-sm font-bold text-teal-800 dark:text-teal-200">{{ data.config.availableShareCount | number }} shares</span>
            </div>
            <div>
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Current Valuation Price</span>
              <span class="text-sm font-bold text-emerald-700 dark:text-emerald-300">{{ data.config.sharePriceBdt | currency:'BDT':'symbol':'1.0-0' }} / share</span>
            </div>
            <div>
              <span class="text-gray-500 dark:text-gray-400 block font-medium">Min Purchase</span>
              <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ data.config.minimumPurchaseShares }} shares</span>
            </div>
          </div>

          <!-- Valuation Mode Fields -->
          <div *ngIf="actionType() === 'valuation'" class="space-y-4">
            <div class="p-3.5 rounded-xl bg-indigo-50 dark:bg-indigo-950/40 border border-indigo-200/60 dark:border-indigo-800/40 flex items-center justify-between text-xs">
              <div>
                <span class="text-gray-500 dark:text-gray-400 block font-medium">Current Share Price</span>
                <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ data.config.sharePriceBdt | currency:'BDT':'symbol':'1.0-0' }}</span>
              </div>
              <div>
                <span class="text-gray-500 dark:text-gray-400 block font-medium">Current Farm Valuation</span>
                <span class="text-sm font-bold text-indigo-700 dark:text-indigo-300">{{ data.config.totalValuationBdt | currency:'BDT':'symbol':'1.0-0' }}</span>
              </div>
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                New Price Per Share (BDT) <span class="text-red-500">*</span>
              </label>
              <input type="number" formControlName="newSharePriceBdt" placeholder="e.g. 5500" min="1" step="0.01"
                     class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
            </div>

            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Reason for Revaluation
              </label>
              <textarea formControlName="valuationNotes" rows="3" placeholder="e.g. Annual farm performance review: herd expanded by 30%, revenue up 22%."
                        class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500 resize-none"></textarea>
            </div>
          </div>

          <!-- Purchase, Sell, or Transfer Mode Fields -->
          <div *ngIf="actionType() !== 'valuation'" class="space-y-4">

            <!-- Primary Investor Selector -->
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                {{ actionType() === 'transfer' ? 'From Investor (Transferor)' : 'Investor' }} <span class="text-red-500">*</span>
              </label>
              <select formControlName="investorId" (change)="onInvestorChanged()"
                      class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
                <option value="" disabled>Select an investor</option>
                <option *ngFor="let inv of data.investors" [value]="inv.id">
                  {{ inv.name }} {{ inv.phone ? '(' + inv.phone + ')' : '' }}
                </option>
              </select>
            </div>

            <!-- Target Investor (Transfer Mode Only) -->
            <div *ngIf="actionType() === 'transfer'" class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                To Investor (Recipient) <span class="text-red-500">*</span>
              </label>
              <select formControlName="toInvestorId"
                      class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
                <option value="" disabled>Select recipient investor</option>
                <option *ngFor="let inv of recipientInvestors()" [value]="inv.id">
                  {{ inv.name }} {{ inv.phone ? '(' + inv.phone + ')' : '' }}
                </option>
              </select>
            </div>

            <!-- Investor Holding Context Badge (if Sell or Transfer) -->
            <div *ngIf="(actionType() === 'sell' || actionType() === 'transfer') && selectedInvestorHolding()"
                 class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/40 flex items-center justify-between text-xs">
              <div>
                <span class="text-gray-500 dark:text-gray-400 block font-medium">Currently Owned</span>
                <span class="text-sm font-bold text-amber-800 dark:text-amber-200">{{ selectedInvestorHolding()?.shareCount | number }} shares</span>
              </div>
              <div>
                <span class="text-gray-500 dark:text-gray-400 block font-medium">Avg Purchase Price</span>
                <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ selectedInvestorHolding()?.averagePurchasePriceBdt | currency:'BDT':'symbol':'1.0-0' }}</span>
              </div>
              <div>
                <span class="text-gray-500 dark:text-gray-400 block font-medium">Total Value</span>
                <span class="text-sm font-bold text-teal-700 dark:text-teal-300">{{ (selectedInvestorHolding()?.shareCount || 0) * data.config.sharePriceBdt | currency:'BDT':'symbol':'1.0-0' }}</span>
              </div>
            </div>

            <!-- Share Count & Price Per Share -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                  Number of Shares <span class="text-red-500">*</span>
                </label>
                <input type="number" formControlName="shareCount" placeholder="e.g. 50" min="1" step="1"
                       class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
              </div>

              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                  Price Per Share (BDT) <span class="text-red-500">*</span>
                </label>
                <input type="number" formControlName="pricePerShareBdt" min="1" step="0.01"
                       class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
              </div>
            </div>

            <!-- Dynamic Total Calculation Banner -->
            <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-800/80 border border-gray-200 dark:border-gray-700 flex items-center justify-between">
              <div>
                <span class="text-xs font-medium text-gray-500 dark:text-gray-400 block">
                  {{ actionType() === 'sell' ? 'Redemption Payout to Investor' : 'Total Investment Amount' }}
                </span>
                <span class="text-lg font-extrabold text-teal-700 dark:text-teal-300">
                  {{ computedTotalAmount() | currency:'BDT':'symbol':'1.0-0' }}
                </span>
              </div>
              <div class="text-right">
                <span class="text-xs font-medium text-gray-500 dark:text-gray-400 block">Ownership Share</span>
                <span class="text-sm font-bold text-gray-800 dark:text-gray-200">
                  {{ computedPercentage() | number:'1.2-2' }}% of farm
                </span>
              </div>
            </div>

            <!-- Date & Reference -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                  Transaction Date
                </label>
                <input type="date" formControlName="transactionDate"
                       class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
              </div>

              <div class="space-y-1.5">
                <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                  Reference / Receipt #
                </label>
                <input type="text" formControlName="referenceId" placeholder="e.g. TXN-7890"
                       class="block w-full px-3 py-2.5 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
              </div>
            </div>

            <!-- Notes -->
            <div class="space-y-1.5">
              <label class="block text-xs font-bold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                Notes
              </label>
              <input type="text" formControlName="notes" placeholder="Optional transaction memo"
                     class="block w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-xl text-sm bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-teal-500 focus:border-teal-500">
            </div>

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
            <span>{{ isSubmitting() ? 'Processing...' : getSubmitButtonText() }}</span>
          </button>
        </div>
      </form>
    </div>
  `
})
export class BuySellSharesDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private financeService = inject(FinanceService);
  private dialogRef = inject(MatDialogRef<BuySellSharesDialogComponent>);
  public data: BuySellSharesDialogData = inject(MAT_DIALOG_DATA);

  public isSubmitting = signal(false);
  public error = signal<string | null>(null);
  public actionType = signal<ShareActionType>('purchase');

  public selectedInvestorHolding = signal<ShareHolding | null>(null);

  public form!: FormGroup;

  ngOnInit(): void {
    this.actionType.set(this.data.actionType);

    const todayStr = new Date().toISOString().split('T')[0];

    this.form = this.fb.group({
      investorId: [this.data.selectedInvestorId ?? '', Validators.required],
      toInvestorId: [''],
      shareCount: [10, [Validators.required, Validators.min(1)]],
      pricePerShareBdt: [this.data.config.sharePriceBdt, [Validators.required, Validators.min(1)]],
      transactionDate: [todayStr],
      referenceId: [''],
      notes: [''],
      newSharePriceBdt: [this.data.config.sharePriceBdt],
      valuationNotes: ['']
    });

    if (this.data.selectedInvestorId) {
      this.onInvestorChanged();
    }
  }

  setActionType(type: ShareActionType): void {
    this.actionType.set(type);
    this.error.set(null);
    this.onInvestorChanged();
  }

  recipientInvestors(): Investor[] {
    const currentFrom = this.form?.get('investorId')?.value;
    return this.data.investors.filter(i => i.id !== currentFrom);
  }

  onInvestorChanged(): void {
    const invId = this.form?.get('investorId')?.value;
    if (!invId) {
      this.selectedInvestorHolding.set(null);
      return;
    }

    const holding = this.data.shareholders?.find(s => s.investorId === invId) ?? null;
    this.selectedInvestorHolding.set(holding);

    if (this.actionType() === 'sell' && holding) {
      this.form.patchValue({
        shareCount: Math.min(10, holding.shareCount)
      });
    }
  }

  computedTotalAmount(): number {
    const count = this.form?.get('shareCount')?.value || 0;
    const price = this.form?.get('pricePerShareBdt')?.value || 0;
    return count * price;
  }

  computedPercentage(): number {
    const count = this.form?.get('shareCount')?.value || 0;
    const total = this.data.config.totalShares;
    return total > 0 ? (count / total) * 100 : 0;
  }

  getHeaderIcon(): string {
    switch (this.actionType()) {
      case 'purchase': return 'add_shopping_cart';
      case 'sell': return 'remove_shopping_cart';
      case 'transfer': return 'swap_horiz';
      case 'valuation': return 'trending_up';
    }
  }

  getHeaderTitle(): string {
    switch (this.actionType()) {
      case 'purchase': return 'Issue / Purchase Farm Shares';
      case 'sell': return 'Redeem / Sell Shares Back';
      case 'transfer': return 'Transfer Shares Between Investors';
      case 'valuation': return 'Update Share Valuation Price';
    }
  }

  getHeaderSubtitle(): string {
    switch (this.actionType()) {
      case 'purchase': return 'Allocate shares to an investor from the available pool';
      case 'sell': return 'Buy back shares from an investor into the farm pool';
      case 'transfer': return 'Transfer ownership of shares from one partner to another';
      case 'valuation': return 'Set a new official market valuation price per share';
    }
  }

  getSubmitButtonText(): string {
    switch (this.actionType()) {
      case 'purchase': return 'Confirm Share Purchase';
      case 'sell': return 'Process Share Redemption';
      case 'transfer': return 'Complete Share Transfer';
      case 'valuation': return 'Update Valuation';
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    this.error.set(null);

    const val = this.form.value;

    if (this.actionType() === 'valuation') {
      const req: UpdateShareValuationRequest = {
        newSharePriceBdt: Number(val.newSharePriceBdt),
        valuationNotes: val.valuationNotes ? String(val.valuationNotes).trim() : null
      };
      this.financeService.updateShareValuation(this.data.farmId, req).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
      return;
    }

    if (this.actionType() === 'purchase') {
      const req: PurchaseSharesRequest = {
        investorId: val.investorId,
        shareCount: Number(val.shareCount),
        pricePerShareBdt: Number(val.pricePerShareBdt),
        transactionDate: val.transactionDate,
        referenceId: val.referenceId ? String(val.referenceId).trim() : null,
        notes: val.notes ? String(val.notes).trim() : null
      };
      this.financeService.purchaseShares(this.data.farmId, req).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
      return;
    }

    if (this.actionType() === 'sell') {
      const req: SellSharesRequest = {
        investorId: val.investorId,
        shareCount: Number(val.shareCount),
        pricePerShareBdt: Number(val.pricePerShareBdt),
        transactionDate: val.transactionDate,
        referenceId: val.referenceId ? String(val.referenceId).trim() : null,
        notes: val.notes ? String(val.notes).trim() : null
      };
      this.financeService.sellShares(this.data.farmId, req).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
      return;
    }

    if (this.actionType() === 'transfer') {
      const req: TransferSharesRequest = {
        fromInvestorId: val.investorId,
        toInvestorId: val.toInvestorId,
        shareCount: Number(val.shareCount),
        pricePerShareBdt: Number(val.pricePerShareBdt),
        transactionDate: val.transactionDate,
        referenceId: val.referenceId ? String(val.referenceId).trim() : null,
        notes: val.notes ? String(val.notes).trim() : null
      };
      this.financeService.transferShares(this.data.farmId, req).subscribe({
        next: (res) => {
          this.isSubmitting.set(false);
          this.dialogRef.close(res);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.error.set(parseApiError(err));
        }
      });
      return;
    }
  }
}
