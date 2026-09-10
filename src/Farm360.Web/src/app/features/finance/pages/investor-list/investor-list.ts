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
import { InvestorFormDialogComponent } from '../../components/investor-form-dialog/investor-form-dialog';
import { InvestorTransactionDialogComponent } from '../../components/investor-transaction-dialog/investor-transaction-dialog';
import { Investor } from '../../models/finance.model';

@Component({
  selector: 'app-investor-list',
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
    EmptyStateComponent,
    CurrencyPipe,
    DatePipe
  ],
  template: `
    <app-page-header 
      title="Investors & Equity" 
      description="Manage farm equity partners, track capital injections, profit distributions, and share ratios."
      breadcrumbActiveNode="Investors">
      <div actions class="flex items-center gap-2">
        <a routerLink="/finance/investors/pnl" mat-stroked-button class="!rounded-xl !px-4 !py-2 !border-teal-300 dark:!border-teal-700 !text-teal-700 dark:!text-teal-300 hover:!bg-teal-50 dark:hover:!bg-teal-950/40 flex items-center gap-1.5 transition-all">
          <mat-icon class="!text-[18px]">query_stats</mat-icon>
          <span>Profit & Loss Sharing</span>
        </a>

        <button mat-flat-button color="primary" (click)="openInvestorDialog()" class="!rounded-xl !px-5 !py-2 !bg-gradient-to-r !from-teal-600 !to-emerald-600 hover:!from-teal-700 hover:!to-emerald-700 !text-white flex items-center gap-2 shadow-sm shadow-teal-500/30 transition-all">
          <mat-icon class="!text-[20px]">person_add</mat-icon>
          <span>New Investor</span>
        </button>
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
        <a routerLink="/finance/reports/trial-balance" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">balance</mat-icon> Trial Balance
        </a>
        <a routerLink="/finance/reports/balance-sheet" 
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">account_balance_wallet</mat-icon> Balance Sheet
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

      <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

      <!-- Content -->
      <div *ngIf="!isLoading()" class="space-y-6">

        <!-- Top Stat Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Active Investors</p>
                <h3 class="text-2xl font-bold text-gray-900 dark:text-white mt-1">{{ totalActiveInvestors() }}</h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center shadow-md shadow-teal-500/20">
                <mat-icon>groups</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-3">Equity partners in this farm</p>
          </div>

          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Total Capital Pool</p>
                <h3 class="text-2xl font-bold text-emerald-600 dark:text-emerald-400 mt-1">
                  {{ totalActiveCapital() | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
                <mat-icon>account_balance</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-3">Active net invested capital</p>
          </div>

          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Profit Distributed</p>
                <h3 class="text-2xl font-bold text-indigo-600 dark:text-indigo-400 mt-1">
                  {{ totalProfitPaid() | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 text-white flex items-center justify-center shadow-md shadow-indigo-500/20">
                <mat-icon>payments</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-3">Paid dividends to date</p>
          </div>

          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-5 border border-gray-100 dark:border-gray-800/50 shadow-sm relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Capital Withdrawn</p>
                <h3 class="text-2xl font-bold text-amber-600 dark:text-amber-400 mt-1">
                  {{ totalCapitalWithdrawn() | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
              </div>
              <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500 to-orange-600 text-white flex items-center justify-center shadow-md shadow-amber-500/20">
                <mat-icon>money_off</mat-icon>
              </div>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400 mt-3">Capital returned to investors</p>
          </div>

        </div>

        <!-- Filter & Search Controls -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl p-4 border border-gray-100 dark:border-gray-800/50 shadow-sm flex flex-col sm:flex-row items-center justify-between gap-4">
          <div class="relative w-full sm:w-80">
            <mat-icon class="absolute left-3 top-2.5 text-gray-400 !text-[20px]">search</mat-icon>
            <input type="text" [ngModel]="searchQuery()" (ngModelChange)="searchQuery.set($event)"
                   placeholder="Search by name, phone, or email..."
                   class="w-full pl-10 pr-4 py-2 text-sm bg-gray-50 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-700 rounded-xl focus:ring-2 focus:ring-teal-500 focus:border-teal-500 text-gray-900 dark:text-white transition-all">
          </div>

          <div class="flex items-center gap-3 self-end sm:self-center">
            <label class="flex items-center gap-2 cursor-pointer text-xs font-semibold text-gray-600 dark:text-gray-300 select-none">
              <input type="checkbox" [checked]="showInactive()" (change)="toggleShowInactive()"
                     class="rounded border-gray-300 text-teal-600 focus:ring-teal-500 h-4 w-4">
              <span>Show Inactive</span>
            </label>
          </div>
        </div>

        <!-- Empty State -->
        <app-empty-state 
          *ngIf="!filteredInvestors() || filteredInvestors().length === 0"
          icon="groups"
          title="No Investors Found"
          description="There are no equity investors registered for this farm yet. Add an investor to track capital and profit sharing."
          actionText="Register First Investor"
          (action)="openInvestorDialog()">
        </app-empty-state>

        <!-- Investors Grid -->
        <div *ngIf="filteredInvestors() && filteredInvestors().length > 0" 
             class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          
          <div *ngFor="let investor of filteredInvestors()" 
               class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6 flex flex-col justify-between transition-all hover:shadow-md">
            
            <!-- Background Watermark -->
            <mat-icon class="absolute -right-4 -bottom-4 text-[100px] text-teal-500/5 rotate-[-10deg] pointer-events-none">groups</mat-icon>

            <!-- Top Row: Avatar & Status -->
            <div>
              <div class="flex justify-between items-start mb-5">
                <div class="flex items-center gap-3">
                  <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-teal-500 to-emerald-600 text-white flex items-center justify-center font-bold text-lg shadow-md shadow-teal-500/20">
                    {{ investor.name.charAt(0).toUpperCase() }}
                  </div>
                  <div>
                    <h3 class="text-base font-bold text-gray-900 dark:text-white m-0">{{ investor.name }}</h3>
                    <p class="text-xs text-gray-400 dark:text-gray-500 m-0 mt-0.5 flex items-center gap-1">
                      <mat-icon class="!text-[13px] !w-[13px] !h-[13px]">calendar_today</mat-icon>
                      Joined {{ investor.investmentDate | date:'mediumDate' }}
                    </p>
                  </div>
                </div>

                <div class="flex items-center gap-1.5">
                  <button (click)="openEditInvestorDialog(investor)" title="Edit Investor"
                          class="p-1.5 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors">
                    <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">edit</mat-icon>
                  </button>

                  <button (click)="toggleInvestorStatus(investor)" [title]="investor.isActive ? 'Deactivate Investor' : 'Activate Investor'"
                          class="px-2.5 py-1 text-xs font-semibold rounded-full transition-colors"
                          [ngClass]="investor.isActive ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 hover:bg-emerald-200' : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400 hover:bg-gray-200'">
                    {{ investor.isActive ? 'Active' : 'Inactive' }}
                  </button>
                </div>
              </div>

              <!-- Contact Pills -->
              <div *ngIf="investor.phone || investor.email" class="flex flex-wrap gap-2 mb-4 text-xs text-gray-500 dark:text-gray-400">
                <span *ngIf="investor.phone" class="inline-flex items-center gap-1 bg-gray-50 dark:bg-gray-700/50 px-2 py-0.5 rounded-md">
                  <mat-icon class="!text-[12px] !w-[12px] !h-[12px]">phone</mat-icon>
                  {{ investor.phone }}
                </span>
                <span *ngIf="investor.email" class="inline-flex items-center gap-1 bg-gray-50 dark:bg-gray-700/50 px-2 py-0.5 rounded-md truncate max-w-[180px]">
                  <mat-icon class="!text-[12px] !w-[12px] !h-[12px]">email</mat-icon>
                  {{ investor.email }}
                </span>
              </div>

              <!-- Metrics Section -->
              <div class="space-y-3 pt-2 pb-4 border-t border-b border-gray-100 dark:border-gray-700/50">
                <div class="flex justify-between items-center">
                  <span class="text-xs font-medium text-gray-500 dark:text-gray-400">Current Capital</span>
                  <span class="text-base font-bold text-emerald-600 dark:text-emerald-400">
                    {{ investor.currentCapitalBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </span>
                </div>

                <div class="flex justify-between items-center">
                  <span class="text-xs font-medium text-gray-500 dark:text-gray-400">Profit Share Ratio</span>
                  <div class="text-right">
                    <span class="text-sm font-bold text-teal-700 dark:text-teal-300">
                      {{ investor.effectiveSharePercentage }}%
                    </span>
                    <span class="text-[10px] text-gray-400 block">
                      {{ investor.agreedProfitSharePercentage ? 'Contract Agreed' : 'Proportional Capital' }}
                    </span>
                  </div>
                </div>

                <div class="flex justify-between items-center text-xs">
                  <span class="text-gray-400">Total Invested / Withdrawn</span>
                  <span class="text-gray-600 dark:text-gray-300">
                    {{ investor.totalInvestedBdt | currency:'BDT ':'symbol':'1.0-0' }} / 
                    <span class="text-amber-600">{{ investor.totalWithdrawnBdt | currency:'BDT ':'symbol':'1.0-0' }}</span>
                  </span>
                </div>

                <div class="flex justify-between items-center text-xs">
                  <span class="text-gray-400">Total Profit Paid</span>
                  <span class="font-semibold text-indigo-600 dark:text-indigo-400">
                    {{ investor.totalProfitPaidBdt | currency:'BDT ':'symbol':'1.0-0' }}
                  </span>
                </div>
              </div>

              <!-- Notes (if any) -->
              <p *ngIf="investor.notes" class="text-xs text-gray-400 italic mt-3 mb-0 line-clamp-2">
                "{{ investor.notes }}"
              </p>

              <!-- Recent Activity Accordion/Toggle -->
              <div class="mt-4">
                <button type="button" (click)="toggleHistory(investor.id)"
                        class="text-xs font-semibold text-gray-500 dark:text-gray-400 hover:text-teal-600 dark:hover:text-teal-400 flex items-center justify-between w-full py-1">
                  <span class="flex items-center gap-1">
                    <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">history</mat-icon>
                    Transactions ({{ investor.transactions?.length || 0 }})
                  </span>
                  <mat-icon class="!text-[16px] !w-[16px] !h-[16px] transition-transform"
                            [ngClass]="expandedHistoryId() === investor.id ? 'rotate-180' : ''">
                    expand_more
                  </mat-icon>
                </button>

                <!-- Collapsible Transaction Mini-List -->
                <div *ngIf="expandedHistoryId() === investor.id" 
                     class="mt-2 space-y-2 max-h-48 overflow-y-auto custom-scrollbar p-2 bg-gray-50 dark:bg-gray-900/50 rounded-xl text-xs">
                  <div *ngIf="!investor.transactions || investor.transactions.length === 0" class="text-center py-2 text-gray-400">
                    No transactions recorded
                  </div>

                  <div *ngFor="let tx of investor.transactions" 
                       class="flex items-center justify-between py-1.5 border-b border-gray-200/50 dark:border-gray-800 last:border-0">
                    <div>
                      <span class="font-bold block" [ngClass]="getTxColor(tx.type)">{{ formatTxType(tx.type) }}</span>
                      <span class="text-[10px] text-gray-400">{{ tx.transactionDate | date:'mediumDate' }}</span>
                      <span *ngIf="tx.referenceId" class="text-[10px] text-gray-400 ml-1">#{{ tx.referenceId }}</span>
                    </div>
                    <span class="font-bold" [ngClass]="getTxColor(tx.type)">
                      {{ tx.amountBdt | currency:'BDT ':'symbol':'1.0-0' }}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Action Buttons Footer -->
            <div class="mt-5 pt-4 border-t border-gray-100 dark:border-gray-700/50 flex flex-wrap gap-2">
              <button (click)="openTransactionDialog(investor, 'CapitalContribution')"
                      class="flex-1 py-2 px-2.5 text-xs font-bold text-white bg-gradient-to-r from-teal-500 to-emerald-600 rounded-xl hover:from-teal-600 hover:to-emerald-700 transition-all shadow-sm shadow-teal-500/20 flex items-center justify-center gap-1">
                <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">add</mat-icon>
                <span>Capital</span>
              </button>

              <button (click)="openTransactionDialog(investor, 'CapitalWithdrawal')"
                      [disabled]="investor.currentCapitalBdt <= 0"
                      class="py-2 px-2.5 text-xs font-bold text-amber-700 dark:text-amber-300 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/40 rounded-xl hover:bg-amber-100 dark:hover:bg-amber-900/40 disabled:opacity-40 disabled:cursor-not-allowed transition-all flex items-center justify-center gap-1">
                <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">remove</mat-icon>
                <span>Withdraw</span>
              </button>

              <button (click)="openTransactionDialog(investor, 'ProfitDistribution')"
                      class="py-2 px-2.5 text-xs font-bold text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/30 border border-indigo-200 dark:border-indigo-800/40 rounded-xl hover:bg-indigo-100 dark:hover:bg-indigo-900/40 transition-all flex items-center justify-center gap-1">
                <mat-icon class="!text-[15px] !w-[15px] !h-[15px]">payments</mat-icon>
                <span>Distribute</span>
              </button>
            </div>

          </div>

        </div>

      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvestorListComponent {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private dialog = inject(MatDialog);

  private refreshTrigger$ = new BehaviorSubject<void>(undefined);
  private showInactiveSubject$ = new BehaviorSubject<boolean>(false);

  readonly searchQuery = signal('');
  readonly showInactive = toSignal(this.showInactiveSubject$, { initialValue: false });
  readonly expandedHistoryId = signal<string | null>(null);

  private readonly investors$ = combineLatest([
    this.refreshTrigger$,
    this.workingContextService.currentFarm$,
    this.showInactiveSubject$
  ]).pipe(
    switchMap(([_, farm, includeInactive]) => {
      if (!farm) return of([] as Investor[]);
      return this.financeService.getInvestors(farm.id, includeInactive).pipe(
        catchError(() => of([] as Investor[]))
      );
    })
  );

  readonly investors = toSignal(this.investors$);
  readonly isLoading = computed(() => this.investors() === undefined);

  readonly filteredInvestors = computed(() => {
    const list = this.investors() || [];
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return list;

    return list.filter(i =>
      i.name.toLowerCase().includes(q) ||
      (i.phone && i.phone.toLowerCase().includes(q)) ||
      (i.email && i.email.toLowerCase().includes(q))
    );
  });

  readonly totalActiveInvestors = computed(() => {
    return (this.investors() || []).filter(i => i.isActive).length;
  });

  readonly totalActiveCapital = computed(() => {
    return (this.investors() || []).filter(i => i.isActive).reduce((sum, i) => sum + i.currentCapitalBdt, 0);
  });

  readonly totalProfitPaid = computed(() => {
    return (this.investors() || []).reduce((sum, i) => sum + i.totalProfitPaidBdt, 0);
  });

  readonly totalCapitalWithdrawn = computed(() => {
    return (this.investors() || []).reduce((sum, i) => sum + i.totalWithdrawnBdt, 0);
  });

  toggleShowInactive(): void {
    this.showInactiveSubject$.next(!this.showInactiveSubject$.value);
  }

  toggleHistory(investorId: string): void {
    this.expandedHistoryId.update(id => id === investorId ? null : investorId);
  }

  getTxColor(type: string): string {
    switch (type) {
      case 'CapitalContribution': return 'text-emerald-600 dark:text-emerald-400';
      case 'CapitalWithdrawal': return 'text-amber-600 dark:text-amber-400';
      case 'ProfitDistribution': return 'text-indigo-600 dark:text-indigo-400';
      default: return 'text-gray-600';
    }
  }

  formatTxType(type: string): string {
    switch (type) {
      case 'CapitalContribution': return '+ Capital Contribution';
      case 'CapitalWithdrawal': return '- Capital Withdrawal';
      case 'ProfitDistribution': return '★ Profit Payout';
      default: return type;
    }
  }

  openInvestorDialog(): void {
    const dialogRef = this.dialog.open(InvestorFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel']
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.refreshTrigger$.next();
    });
  }

  openEditInvestorDialog(investor: Investor): void {
    const dialogRef = this.dialog.open(InvestorFormDialogComponent, {
      width: '600px',
      disableClose: true,
      panelClass: ['premium-dialog-panel'],
      data: investor
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.refreshTrigger$.next();
    });
  }

  openTransactionDialog(investor: Investor, defaultType?: 'CapitalContribution' | 'CapitalWithdrawal' | 'ProfitDistribution'): void {
    const dialogRef = this.dialog.open(InvestorTransactionDialogComponent, {
      width: '540px',
      disableClose: true,
      panelClass: ['premium-dialog-panel'],
      data: { investor, defaultType }
    });

    dialogRef.afterClosed().subscribe(res => {
      if (res) this.refreshTrigger$.next();
    });
  }

  toggleInvestorStatus(investor: Investor): void {
    const farmId = this.workingContextService.currentFarmValue?.id;
    if (!farmId) return;

    this.financeService.toggleInvestorStatus(farmId, investor.id, !investor.isActive).subscribe({
      next: () => this.refreshTrigger$.next()
    });
  }
}
