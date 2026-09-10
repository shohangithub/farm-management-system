import { Component, ChangeDetectionStrategy, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { BalanceSheet } from '../../models/finance.model';

@Component({
  selector: 'app-balance-sheet',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    DatePipe
  ],
  template: `
    <app-page-header 
      title="Balance Sheet" 
      description="Statement of financial position showing assets, liabilities, and owner/investor equity."
      breadcrumbActiveNode="Balance Sheet">
      <div actions class="flex items-center gap-3">
        <a mat-stroked-button routerLink="/finance"
          class="rounded-xl border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-200 flex items-center gap-2 px-3 py-1.5 text-xs">
          <mat-icon class="text-sm">arrow_back</mat-icon>
          <span>Overview</span>
        </a>
        <form [formGroup]="filterForm" class="flex items-center gap-2">
          <div class="flex items-center gap-1.5 px-3 py-1.5 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-xs font-medium">
            <span class="text-gray-500 dark:text-gray-400">As of:</span>
            <input 
              type="date" 
              formControlName="asOfDate" 
              class="bg-transparent text-gray-800 dark:text-gray-200 text-xs font-semibold outline-none cursor-pointer" />
          </div>
        </form>
        <button mat-flat-button (click)="printReport()"
          class="rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white flex items-center gap-2 px-3 py-1.5 text-xs font-semibold shadow-sm shadow-emerald-600/20">
          <mat-icon class="text-sm">print</mat-icon>
          <span>Print / PDF</span>
        </button>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="px-6 py-4 mx-auto max-w-7xl space-y-6">

      <!-- Sub-Navigation Bar -->
      <div class="flex items-center gap-2 border-b border-gray-200 dark:border-gray-700/60 pb-3 overflow-x-auto">
        <a routerLink="/finance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">dashboard</mat-icon> Overview
        </a>
        <a routerLink="/finance/transactions" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">receipt_long</mat-icon> General Ledger
        </a>
        <a routerLink="/finance/reports/monthly-pnl" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">calendar_view_month</mat-icon> Monthly P&L
        </a>
        <a routerLink="/finance/reports/trial-balance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">balance</mat-icon> Trial Balance
        </a>
        <a routerLink="/finance/reports/balance-sheet" 
          class="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60 flex items-center gap-2 whitespace-nowrap">
          <mat-icon class="text-base">account_balance_wallet</mat-icon> Balance Sheet
        </a>
        <a routerLink="/finance/loans" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance</mat-icon> Loans & Liabilities
        </a>
        <a routerLink="/finance/investors" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">groups</mat-icon> Investors & Equity
        </a>
      </div>

      <app-empty-state 
        *ngIf="!bsData()"
        icon="account_balance"
        title="No Financial Records Found"
        description="There is no balance sheet data available for this farm as of the selected date.">
      </app-empty-state>

      <div *ngIf="bsData() as sheet" class="space-y-6">

        <!-- Accounting Equation Verification Banner -->
        <div class="p-4 rounded-2xl border backdrop-blur-xl transition-all"
             [ngClass]="sheet.isBalanced ? 'bg-emerald-50/70 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800/60' : 'bg-rose-50/70 dark:bg-rose-950/30 border-rose-200 dark:border-rose-800/60'">
          <div class="flex items-center justify-between flex-wrap gap-3">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-xl flex items-center justify-center text-white"
                   [ngClass]="sheet.isBalanced ? 'bg-gradient-to-br from-emerald-500 to-teal-600 shadow-md shadow-emerald-500/20' : 'bg-gradient-to-br from-rose-500 to-amber-600 shadow-md shadow-rose-500/20'">
                <mat-icon class="text-base">{{ sheet.isBalanced ? 'verified' : 'error_outline' }}</mat-icon>
              </div>
              <div>
                <h4 class="text-sm font-bold text-gray-900 dark:text-white flex items-center gap-2">
                  <span>Fundamental Accounting Equation:</span>
                  <span class="font-mono text-emerald-700 dark:text-emerald-300">Assets = Liabilities + Equity</span>
                </h4>
                <p class="text-xs text-gray-600 dark:text-gray-300 mt-0.5">
                  <span class="font-medium text-gray-500 dark:text-gray-400">As of {{ sheet.asOfDate | date:'mediumDate' }}:</span>
                  <span class="font-mono font-semibold ml-1">{{ sheet.totalAssetsBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
                  (Assets) =
                  <span class="font-mono font-semibold">{{ sheet.liabilities.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
                  (Liab) +
                  <span class="font-mono font-semibold">{{ sheet.equity.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
                  (Equity)
                </p>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <span *ngIf="sheet.isBalanced" class="px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 dark:bg-emerald-900/60 text-emerald-800 dark:text-emerald-200 flex items-center gap-1.5 shadow-sm">
                <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">check_circle</mat-icon> In Exact Balance
              </span>
              <span *ngIf="!sheet.isBalanced" class="px-3 py-1 rounded-full text-xs font-bold bg-rose-100 dark:bg-rose-900/60 text-rose-800 dark:text-rose-200 flex items-center gap-1.5 shadow-sm">
                <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">warning</mat-icon> Variance: {{ sheet.differenceBdt | currency:'BDT ':'symbol':'1.2-2' }}
              </span>
            </div>
          </div>
        </div>

        <!-- 4 KPI Summary Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- Total Assets -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Assets</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ sheet.totalAssetsBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Cash & Equivalents</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
                <mat-icon class="text-lg">account_balance_wallet</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">account_balance_wallet</mat-icon>
          </div>

          <!-- Total Liabilities -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Liabilities</p>
                <h3 class="text-2xl font-black text-amber-600 dark:text-amber-400 mt-1">
                  {{ sheet.liabilities.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Loans & Borrowings</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center shadow-md shadow-amber-500/20">
                <mat-icon class="text-lg">credit_card</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-amber-500/5 rotate-[-10deg] pointer-events-none">credit_card</mat-icon>
          </div>

          <!-- Total Equity -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Equity</p>
                <h3 class="text-2xl font-black text-purple-600 dark:text-purple-400 mt-1">
                  {{ sheet.equity.totalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Capital & Retained Earnings</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-purple-500 to-violet-600 text-white flex items-center justify-center shadow-md shadow-purple-500/20">
                <mat-icon class="text-lg">pie_chart</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-purple-500/5 rotate-[-10deg] pointer-events-none">pie_chart</mat-icon>
          </div>

          <!-- Net Working Capital -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Net Working Capital</p>
                <h3 class="text-2xl font-black mt-1"
                    [ngClass]="sheet.netWorkingCapitalBdt >= 0 ? 'text-teal-600 dark:text-teal-400' : 'text-rose-600 dark:text-rose-400'">
                  {{ sheet.netWorkingCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Current Assets − Liabilities</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-teal-500 to-cyan-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20">
                <mat-icon class="text-lg">trending_up</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-teal-500/5 rotate-[-10deg] pointer-events-none">trending_up</mat-icon>
          </div>
        </div>

        <!-- 2-Column Balance Sheet Presentation -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">

          <!-- Left Column: ASSETS -->
          <div class="space-y-6">
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
              <div class="p-5 border-b border-gray-100 dark:border-gray-700/60 flex items-center justify-between bg-emerald-50/30 dark:bg-emerald-950/20">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-sm">
                    <mat-icon class="text-base">account_balance_wallet</mat-icon>
                  </div>
                  <div>
                    <h3 class="text-base font-black text-gray-900 dark:text-white">Assets</h3>
                    <p class="text-xs text-gray-500 dark:text-gray-400">Current liquid farm resources</p>
                  </div>
                </div>
                <span class="text-xs font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300 bg-emerald-100/70 dark:bg-emerald-900/50 px-2.5 py-1 rounded-full">
                  Current Assets
                </span>
              </div>

              <div class="p-5 space-y-4">
                <div *ngFor="let line of sheet.assets.lines" 
                     class="flex items-start justify-between p-3 rounded-xl hover:bg-gray-50/70 dark:hover:bg-gray-700/30 transition-colors">
                  <div class="space-y-0.5">
                    <div class="flex items-center gap-2">
                      <span class="font-mono text-xs font-bold text-gray-400 dark:text-gray-500">{{ line.lineCode }}</span>
                      <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ line.lineName }}</span>
                    </div>
                    <p *ngIf="line.notes" class="text-xs text-gray-500 dark:text-gray-400 max-w-sm">{{ line.notes }}</p>
                  </div>
                  <div class="text-right">
                    <span class="font-mono text-base font-black text-gray-900 dark:text-white">
                      {{ line.amountBdt | currency:'BDT ':'symbol':'1.2-2' }}
                    </span>
                  </div>
                </div>

                <!-- Info Notice on Biological Assets -->
                <div class="p-3.5 rounded-xl bg-blue-50/50 dark:bg-blue-950/20 border border-blue-100 dark:border-blue-900/40 text-xs text-blue-700 dark:text-blue-300 flex items-start gap-2.5">
                  <mat-icon class="text-sm !w-4 !h-4 !text-[16px] shrink-0 mt-0.5">info</mat-icon>
                  <div>
                    <span class="font-bold">Biological Assets (Livestock & Feed Inventory):</span>
                    <p class="text-[11px] text-blue-600/80 dark:text-blue-300/80 mt-0.5">
                      Biological livestock valuation and feed inventories are tracked in their dedicated modules and will be integrated into fair-value accounting in a future update.
                    </p>
                  </div>
                </div>
              </div>

              <!-- Total Assets Footer -->
              <div class="p-4 border-t-2 border-b-4 border-gray-900 dark:border-gray-200 bg-gray-50 dark:bg-gray-900/60 flex items-center justify-between font-black">
                <span class="text-sm uppercase tracking-wider text-gray-900 dark:text-white">Total Assets</span>
                <span class="font-mono text-lg text-emerald-700 dark:text-emerald-400">
                  {{ sheet.totalAssetsBdt | currency:'BDT ':'symbol':'1.2-2' }}
                </span>
              </div>
            </div>
          </div>

          <!-- Right Column: LIABILITIES & EQUITY -->
          <div class="space-y-6">

            <!-- Section: LIABILITIES -->
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
              <div class="p-5 border-b border-gray-100 dark:border-gray-700/60 flex items-center justify-between bg-amber-50/30 dark:bg-amber-950/20">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center shadow-sm">
                    <mat-icon class="text-base">credit_card</mat-icon>
                  </div>
                  <div>
                    <h3 class="text-base font-black text-gray-900 dark:text-white">Liabilities</h3>
                    <p class="text-xs text-gray-500 dark:text-gray-400">Outstanding debt and obligations</p>
                  </div>
                </div>
                <span class="text-xs font-bold uppercase tracking-wider text-amber-700 dark:text-amber-300 bg-amber-100/70 dark:bg-amber-900/50 px-2.5 py-1 rounded-full">
                  Debt & Borrowing
                </span>
              </div>

              <div class="p-5 space-y-4">
                <div *ngFor="let line of sheet.liabilities.lines" 
                     class="flex items-start justify-between p-3 rounded-xl hover:bg-gray-50/70 dark:hover:bg-gray-700/30 transition-colors">
                  <div class="space-y-0.5">
                    <div class="flex items-center gap-2">
                      <span class="font-mono text-xs font-bold text-gray-400 dark:text-gray-500">{{ line.lineCode }}</span>
                      <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ line.lineName }}</span>
                    </div>
                    <p *ngIf="line.notes" class="text-xs text-gray-500 dark:text-gray-400 max-w-sm">{{ line.notes }}</p>
                  </div>
                  <div class="text-right">
                    <span class="font-mono text-base font-black text-amber-700 dark:text-amber-400">
                      {{ line.amountBdt | currency:'BDT ':'symbol':'1.2-2' }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="p-3.5 border-t border-gray-200 dark:border-gray-700/80 bg-gray-50/60 dark:bg-gray-900/40 flex items-center justify-between font-bold">
                <span class="text-xs uppercase tracking-wider text-gray-600 dark:text-gray-400">Total Liabilities</span>
                <span class="font-mono text-base text-amber-700 dark:text-amber-400">
                  {{ sheet.liabilities.totalBdt | currency:'BDT ':'symbol':'1.2-2' }}
                </span>
              </div>
            </div>

            <!-- Section: EQUITY -->
            <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
              <div class="p-5 border-b border-gray-100 dark:border-gray-700/60 flex items-center justify-between bg-purple-50/30 dark:bg-purple-950/20">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-purple-500 to-violet-600 text-white flex items-center justify-center shadow-sm">
                    <mat-icon class="text-base">pie_chart</mat-icon>
                  </div>
                  <div>
                    <h3 class="text-base font-black text-gray-900 dark:text-white">Equity & Capital</h3>
                    <p class="text-xs text-gray-500 dark:text-gray-400">Investor capital and retained profits</p>
                  </div>
                </div>
                <span class="text-xs font-bold uppercase tracking-wider text-purple-700 dark:text-purple-300 bg-purple-100/70 dark:bg-purple-900/50 px-2.5 py-1 rounded-full">
                  Owners' Equity
                </span>
              </div>

              <div class="p-5 space-y-4">
                <div *ngFor="let line of sheet.equity.lines" 
                     class="flex items-start justify-between p-3 rounded-xl hover:bg-gray-50/70 dark:hover:bg-gray-700/30 transition-colors">
                  <div class="space-y-0.5">
                    <div class="flex items-center gap-2">
                      <span class="font-mono text-xs font-bold text-gray-400 dark:text-gray-500">{{ line.lineCode }}</span>
                      <span class="text-sm font-bold text-gray-800 dark:text-gray-200">{{ line.lineName }}</span>
                    </div>
                    <p *ngIf="line.notes" class="text-xs text-gray-500 dark:text-gray-400 max-w-sm">{{ line.notes }}</p>
                  </div>
                  <div class="text-right">
                    <span class="font-mono text-base font-black"
                          [ngClass]="line.amountBdt >= 0 ? 'text-purple-700 dark:text-purple-300' : 'text-rose-600 dark:text-rose-400'">
                      {{ line.amountBdt | currency:'BDT ':'symbol':'1.2-2' }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="p-3.5 border-t border-gray-200 dark:border-gray-700/80 bg-gray-50/60 dark:bg-gray-900/40 flex items-center justify-between font-bold">
                <span class="text-xs uppercase tracking-wider text-gray-600 dark:text-gray-400">Total Equity</span>
                <span class="font-mono text-base text-purple-700 dark:text-purple-300">
                  {{ sheet.equity.totalBdt | currency:'BDT ':'symbol':'1.2-2' }}
                </span>
              </div>
            </div>

            <!-- Grand Total: Total Liabilities & Equity -->
            <div class="p-4 rounded-2xl border-t-2 border-b-4 border-gray-900 dark:border-gray-200 bg-white/90 dark:bg-gray-800/90 shadow-sm flex items-center justify-between font-black">
              <span class="text-sm uppercase tracking-wider text-gray-900 dark:text-white">
                Total Liabilities & Equity
              </span>
              <span class="font-mono text-lg text-indigo-700 dark:text-indigo-400">
                {{ sheet.totalLiabilitiesAndEquityBdt | currency:'BDT ':'symbol':'1.2-2' }}
              </span>
            </div>

          </div>

        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BalanceSheetComponent {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);

  private readonly todayStr = new Date().toISOString().split('T')[0];

  readonly filterForm = this.fb.group({
    asOfDate: [this.todayStr]
  });

  private readonly filterChanges$ = new BehaviorSubject<{ asOfDate: string }>({
    asOfDate: this.todayStr
  });

  constructor() {
    this.filterForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(val => {
        if (val.asOfDate) {
          this.filterChanges$.next({ asOfDate: val.asOfDate });
        }
      });
  }

  private readonly bsData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filter, farm]) => {
      if (!farm) return of(null);
      return this.financeService.getBalanceSheet(farm.id, filter.asOfDate).pipe(
        catchError(err => {
          console.error('Error loading balance sheet:', err);
          return of(null);
        })
      );
    })
  );

  readonly bsData = toSignal(this.bsData$);
  readonly isLoading = computed(() => this.bsData() === undefined);

  printReport(): void {
    window.print();
  }
}
