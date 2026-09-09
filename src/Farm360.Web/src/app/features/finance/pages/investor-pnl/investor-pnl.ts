import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';
import { InvestorTransactionDialogComponent } from '../../components/investor-transaction-dialog/investor-transaction-dialog';
import { InvestorPnLSummary, InvestorShare } from '../../models/finance.model';

@Component({
  selector: 'app-investor-pnl',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    PageHeaderComponent,
    LoadingComponent,
    CurrencyPipe
  ],
  template: `
    <app-page-header 
      title="Profit & Loss Sharing" 
      description="Calculates farm operating profitability and computes investor equity shares, allocated profits, and undistributed balances."
      breadcrumbActiveNode="Profit & Loss Sharing">
      <div actions class="flex items-center gap-2">
        <a routerLink="/finance/investors" mat-stroked-button class="!rounded-xl !px-4 !py-2 !border-gray-300 dark:!border-gray-700 !text-gray-700 dark:!text-gray-300 hover:!bg-gray-100 dark:hover:!bg-gray-800 flex items-center gap-1.5 transition-all">
          <mat-icon class="!text-[18px]">arrow_back</mat-icon>
          <span>Investors List</span>
        </a>
      </div>
    </app-page-header>

    <div class="px-6 py-4 mx-auto max-w-7xl space-y-6">

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
        <a routerLink="/finance/loans" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance</mat-icon> Loans & Liabilities
        </a>
        <a routerLink="/finance/investors" 
          class="px-4 py-2 rounded-xl text-xs font-semibold bg-teal-50 dark:bg-teal-950/40 text-teal-700 dark:text-teal-300 border border-teal-200 dark:border-teal-800/60 flex items-center gap-2 whitespace-nowrap">
          <mat-icon class="text-base">groups</mat-icon> Investors & Equity
        </a>
      </div>

      <!-- Filter Controls: Date Range & Quick Presets -->
      <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-4 border border-gray-100 dark:border-gray-800/50 shadow-sm flex flex-col md:flex-row items-center justify-between gap-4">
        
        <div class="flex items-center gap-2 overflow-x-auto w-full md:w-auto">
          <button type="button" (click)="setPreset('all')"
                  [ngClass]="activePreset() === 'all' ? 'bg-teal-600 text-white shadow-sm' : 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200'"
                  class="px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap">
            All Time
          </button>
          <button type="button" (click)="setPreset('year')"
                  [ngClass]="activePreset() === 'year' ? 'bg-teal-600 text-white shadow-sm' : 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200'"
                  class="px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap">
            This Year
          </button>
          <button type="button" (click)="setPreset('month')"
                  [ngClass]="activePreset() === 'month' ? 'bg-teal-600 text-white shadow-sm' : 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200'"
                  class="px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap">
            This Month
          </button>
        </div>

        <div class="flex items-center gap-2 w-full md:w-auto">
          <div class="flex items-center gap-1.5 text-xs text-gray-500">
            <span>From:</span>
            <input type="date" [ngModel]="fromDate()" (ngModelChange)="onFromDateChange($event)"
                   class="px-2.5 py-1.5 bg-gray-50 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-700 rounded-lg text-xs text-gray-900 dark:text-white">
          </div>
          <div class="flex items-center gap-1.5 text-xs text-gray-500">
            <span>To:</span>
            <input type="date" [ngModel]="toDate()" (ngModelChange)="onToDateChange($event)"
                   class="px-2.5 py-1.5 bg-gray-50 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-700 rounded-lg text-xs text-gray-900 dark:text-white">
          </div>
        </div>

      </div>

      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Main Content -->
      <div *ngIf="!isLoading() && summary()" class="space-y-6">

        <!-- 6 KPI Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          
          <!-- Farm Operating Revenue -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Farm Revenue</p>
                <h3 class="text-2xl font-bold text-emerald-600 dark:text-emerald-400 mt-1">
                  {{ summary()!.totalFarmRevenueBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
                <mat-icon>trending_up</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">Excludes capital injections</p>
          </div>

          <!-- Farm Operating Expenses -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Farm Expenses</p>
                <h3 class="text-2xl font-bold text-rose-600 dark:text-rose-400 mt-1">
                  {{ summary()!.totalFarmExpensesBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-rose-500 to-pink-600 text-white flex items-center justify-center shadow-md shadow-rose-500/20">
                <mat-icon>trending_down</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">Excludes investor payouts</p>
          </div>

          <!-- Net Farm Profit / Loss -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Net Farm Profit / Loss</p>
                <h3 class="text-2xl font-bold mt-1"
                    [ngClass]="summary()!.netFarmProfitBdt >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
                  {{ summary()!.netFarmProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl flex items-center justify-center shadow-md text-white"
                   [ngClass]="summary()!.netFarmProfitBdt >= 0 ? 'bg-gradient-to-br from-emerald-500 to-teal-600 shadow-emerald-500/20' : 'bg-gradient-to-br from-rose-500 to-pink-600 shadow-rose-500/20'">
                <mat-icon>{{ summary()!.netFarmProfitBdt >= 0 ? 'savings' : 'warning' }}</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">Gross operating result before charity</p>
          </div>

          <!-- Charity Deduction (5%) -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Charity Deduction ({{ summary()!.charityPercentage }}%)</p>
                <h3 class="text-2xl font-bold text-purple-600 dark:text-purple-400 mt-1">
                  {{ summary()!.charityAmountBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-purple-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-purple-500/20">
                <mat-icon>volunteer_activism</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">5% allocated to charity from positive profit</p>
          </div>

          <!-- Distributable Profit -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Distributable Profit (95%)</p>
                <h3 class="text-2xl font-bold mt-1"
                    [ngClass]="summary()!.distributableProfitBdt >= 0 ? 'text-indigo-600 dark:text-indigo-400' : 'text-rose-600 dark:text-rose-400'">
                  {{ summary()!.distributableProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-500 to-blue-600 text-white flex items-center justify-center shadow-md shadow-indigo-500/20">
                <mat-icon>account_balance_wallet</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">Pool shared among equity investors</p>
          </div>

          <!-- Total Active Capital Pool -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Total Capital Pool</p>
                <h3 class="text-2xl font-bold text-teal-600 dark:text-teal-400 mt-1">
                  {{ summary()!.totalActiveCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-teal-500 to-cyan-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20">
                <mat-icon>pie_chart</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-400 mt-3">From all active equity partners</p>
          </div>

        </div>

        <!-- Profit Sharing Allocation Table -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden">
          
          <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-800 flex items-center justify-between">
            <div>
              <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">Investor Equity & Profit Distribution Breakdown</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 m-0">
                Detailed share allocation of Distributable Profit (after 5% charity) based on agreed contract or active capital ratio
              </p>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs text-gray-700 dark:text-gray-300">
              <thead class="bg-gray-50 dark:bg-gray-800/50 uppercase font-semibold text-[10px] tracking-wider text-gray-500 dark:text-gray-400 border-b border-gray-100 dark:border-gray-800">
                <tr>
                  <th class="px-6 py-3.5">Investor Name</th>
                  <th class="px-4 py-3.5 text-right">Invested Capital</th>
                  <th class="px-4 py-3.5 text-right">Capital %</th>
                  <th class="px-4 py-3.5 text-right">Profit Share %</th>
                  <th class="px-4 py-3.5 text-right">Allocated Share</th>
                  <th class="px-4 py-3.5 text-right">Profit Paid</th>
                  <th class="px-4 py-3.5 text-right">Undistributed Balance</th>
                  <th class="px-4 py-3.5 text-right">Net Equity Value</th>
                  <th class="px-6 py-3.5 text-center">Action</th>
                </tr>
              </thead>

              <tbody class="divide-y divide-gray-100 dark:divide-gray-800">
                <tr *ngFor="let share of summary()!.investorShares" 
                    class="hover:bg-gray-50/70 dark:hover:bg-gray-800/40 transition-colors">
                  
                  <!-- Investor Name & Badge -->
                  <td class="px-6 py-4 font-bold text-gray-900 dark:text-white flex items-center gap-2.5">
                    <div class="w-8 h-8 rounded-lg bg-teal-50 dark:bg-teal-900/30 text-teal-700 dark:text-teal-300 font-bold flex items-center justify-center text-xs">
                      {{ share.investorName.charAt(0).toUpperCase() }}
                    </div>
                    <div>
                      <span class="block">{{ share.investorName }}</span>
                      <span class="text-[10px] font-normal" [ngClass]="share.isActive ? 'text-emerald-500' : 'text-gray-400'">
                        {{ share.isActive ? 'Active Partner' : 'Inactive' }}
                      </span>
                    </div>
                  </td>

                  <!-- Invested Capital -->
                  <td class="px-4 py-4 text-right font-medium">
                    {{ share.investedCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </td>

                  <!-- Capital Share % -->
                  <td class="px-4 py-4 text-right text-gray-500">
                    {{ share.capitalSharePercentage }}%
                  </td>

                  <!-- Effective Profit Share % -->
                  <td class="px-4 py-4 text-right font-bold text-teal-600 dark:text-teal-400">
                    {{ share.effectiveSharePercentage }}%
                  </td>

                  <!-- Allocated Profit / Loss -->
                  <td class="px-4 py-4 text-right font-bold"
                      [ngClass]="share.allocatedProfitBdt >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
                    {{ share.allocatedProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </td>

                  <!-- Total Distributed Paid -->
                  <td class="px-4 py-4 text-right font-medium text-indigo-600 dark:text-indigo-400">
                    {{ share.distributedProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </td>

                  <!-- Undistributed Balance -->
                  <td class="px-4 py-4 text-right">
                    <span class="inline-block px-2.5 py-1 rounded-full font-bold text-[11px]"
                          [ngClass]="share.undistributedProfitBdt > 0 ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300' : (share.undistributedProfitBdt < 0 ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300' : 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400')">
                      {{ share.undistributedProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                    </span>
                  </td>

                  <!-- Net Equity Value -->
                  <td class="px-4 py-4 text-right font-bold text-gray-900 dark:text-white">
                    {{ share.netEquityValueBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </td>

                  <!-- Action -->
                  <td class="px-6 py-4 text-center">
                    <button (click)="openPayoutDialog(share)"
                            title="Distribute Profit"
                            class="px-3 py-1.5 text-[11px] font-bold text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/30 border border-indigo-200 dark:border-indigo-800/40 rounded-lg hover:bg-indigo-100 dark:hover:bg-indigo-900/50 transition-colors inline-flex items-center gap-1">
                      <mat-icon class="!text-[14px] !w-[14px] !h-[14px]">payments</mat-icon>
                      <span>Pay Out</span>
                    </button>
                  </td>

                </tr>
              </tbody>

              <!-- Table Summary Footer -->
              <tfoot class="bg-gray-50/80 dark:bg-gray-800/80 font-bold border-t border-gray-200 dark:border-gray-700">
                <tr>
                  <td class="px-6 py-3.5">Totals</td>
                  <td class="px-4 py-3.5 text-right">{{ summary()!.totalActiveCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
                  <td class="px-4 py-3.5 text-right">100%</td>
                  <td class="px-4 py-3.5 text-right">-</td>
                  <td class="px-4 py-3.5 text-right" [ngClass]="summary()!.distributableProfitBdt >= 0 ? 'text-indigo-600 dark:text-indigo-400' : 'text-rose-600'">
                    {{ summary()!.distributableProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </td>
                  <td class="px-4 py-3.5 text-right text-indigo-600">{{ summary()!.totalDistributedProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
                  <td class="px-4 py-3.5 text-right text-emerald-600">{{ summary()!.totalUndistributedProfitBdt | currency:'BDT ':'symbol':'1.0-0' }}</td>
                  <td class="px-4 py-3.5 text-right">{{ (summary()!.totalActiveCapitalBdt + summary()!.totalUndistributedProfitBdt) | currency:'BDT ':'symbol':'1.0-0' }}</td>
                  <td class="px-6 py-3.5"></td>
                </tr>
              </tfoot>

            </table>
          </div>

        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvestorPnLComponent {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  private refreshTrigger$ = new BehaviorSubject<void>(undefined);
  private dateFilter$ = new BehaviorSubject<{ from?: string; to?: string }>({ from: undefined, to: undefined });

  readonly activePreset = signal<'all' | 'year' | 'month'>('all');
  readonly fromDate = signal<string | undefined>(undefined);
  readonly toDate = signal<string | undefined>(undefined);

  private readonly summary$ = combineLatest([
    this.refreshTrigger$,
    this.workingContextService.currentFarm$,
    this.dateFilter$
  ]).pipe(
    switchMap(([_, farm, filter]) => {
      if (!farm) return of(null);
      return this.financeService.getInvestorPnL(farm.id, filter.from, filter.to).pipe(
        catchError(() => of(null))
      );
    })
  );

  readonly summary = toSignal(this.summary$);
  readonly isLoading = computed(() => this.summary() === undefined);

  setPreset(preset: 'all' | 'year' | 'month'): void {
    this.activePreset.set(preset);
    const now = new Date();

    let from: string | undefined = undefined;
    let to: string | undefined = undefined;

    if (preset === 'year') {
      from = new Date(now.getFullYear(), 0, 1).toISOString().split('T')[0];
      to = new Date(now.getFullYear(), 11, 31).toISOString().split('T')[0];
    } else if (preset === 'month') {
      from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0];
      to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().split('T')[0];
    }

    this.fromDate.set(from);
    this.toDate.set(to);
    this.dateFilter$.next({ from, to });
  }

  onFromDateChange(val: string): void {
    const from = val || undefined;
    this.fromDate.set(from);
    this.activePreset.set('all');
    this.dateFilter$.next({ from, to: this.toDate() });
  }

  onToDateChange(val: string): void {
    const to = val || undefined;
    this.toDate.set(to);
    this.activePreset.set('all');
    this.dateFilter$.next({ from: this.fromDate(), to });
  }

  openPayoutDialog(share: InvestorShare): void {
    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;

    // Fetch the investor to pass into dialog
    this.financeService.getInvestorById(farmId, share.investorId).subscribe(investor => {
      const dialogRef = this.dialog.open(InvestorTransactionDialogComponent, {
        width: '540px',
        disableClose: true,
        panelClass: ['premium-dialog-panel'],
        data: { investor, defaultType: 'ProfitDistribution' }
      });

      dialogRef.afterClosed().subscribe(res => {
        if (res) this.refreshTrigger$.next();
      });
    });
  }
}
