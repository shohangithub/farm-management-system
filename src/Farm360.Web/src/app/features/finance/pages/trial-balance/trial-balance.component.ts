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
import { TrialBalance, TrialBalanceLine } from '../../models/finance.model';

@Component({
  selector: 'app-trial-balance',
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
      title="Trial Balance" 
      description="Comprehensive summary of debit and credit ledger balances ensuring double-entry mathematical integrity."
      breadcrumbActiveNode="Trial Balance">
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
          class="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60 flex items-center gap-2 whitespace-nowrap">
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
          class="px-4 py-2 rounded-xl text-xs font-semibold text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 flex items-center gap-2 whitespace-nowrap transition-colors">
          <mat-icon class="text-base">groups</mat-icon> Investors & Equity
        </a>
      </div>

      <app-empty-state 
        *ngIf="!tbData() || !tbData()?.lines?.length"
        icon="account_balance_wallet"
        title="No Ledger Records Found"
        description="There are no transaction lines recorded as of the selected date. Start by recording revenue or expenses.">
      </app-empty-state>

      <div *ngIf="tbData() as data" class="space-y-6">

        <!-- Top Status & KPI Cards -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <!-- Total Debits -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Debits</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.totalDebitBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Assets, Expenses, Drawings</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-blue-500/20">
                <mat-icon class="text-lg">arrow_downward</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-blue-500/5 rotate-[-10deg] pointer-events-none">account_balance</mat-icon>
          </div>

          <!-- Total Credits -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Total Credits</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.totalCreditBdt | currency:'BDT ':'symbol':'1.0-0' }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">Revenue, Liabilities, Capital</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
                <mat-icon class="text-lg">arrow_upward</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">savings</mat-icon>
          </div>

          <!-- Balance Integrity -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden"
               [ngClass]="data.isBalanced ? 'bg-gradient-to-br from-emerald-50/50 to-teal-50/50 dark:from-emerald-950/20 dark:to-teal-950/20' : 'bg-gradient-to-br from-amber-50/60 to-rose-50/60 dark:from-amber-950/20 dark:to-rose-950/20'">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Ledger Integrity</p>
                <div class="flex items-center gap-2 mt-1">
                  <span *ngIf="data.isBalanced" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-100 dark:bg-emerald-900/50 text-emerald-700 dark:text-emerald-300 flex items-center gap-1">
                    <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">check_circle</mat-icon> Balanced
                  </span>
                  <span *ngIf="!data.isBalanced" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-rose-100 dark:bg-rose-900/50 text-rose-700 dark:text-rose-300 flex items-center gap-1">
                    <mat-icon class="text-xs !w-3.5 !h-3.5 !text-[14px]">warning</mat-icon> Discrepancy
                  </span>
                </div>
                <p class="text-[11px] mt-1 font-semibold" [ngClass]="data.isBalanced ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
                  {{ data.isBalanced ? 'Debits equal Credits (100% in sync)' : 'Difference: ' + (data.differenceBdt | currency:'BDT ':'symbol':'1.2-2') }}
                </p>
              </div>
              <div class="w-11 h-11 rounded-2xl flex items-center justify-center text-white"
                   [ngClass]="data.isBalanced ? 'bg-gradient-to-br from-emerald-500 to-teal-600 shadow-md shadow-emerald-500/20' : 'bg-gradient-to-br from-rose-500 to-amber-600 shadow-md shadow-rose-500/20'">
                <mat-icon class="text-lg">{{ data.isBalanced ? 'verified' : 'priority_high' }}</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">balance</mat-icon>
          </div>

          <!-- Total Accounts -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-5 relative overflow-hidden">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Active Accounts</p>
                <h3 class="text-2xl font-black text-gray-900 dark:text-white mt-1">
                  {{ data.lines.length }}
                </h3>
                <p class="text-[11px] text-gray-400 mt-0.5">As of {{ data.asOfDate | date:'mediumDate' }}</p>
              </div>
              <div class="w-11 h-11 rounded-2xl bg-gradient-to-br from-purple-500 to-violet-600 text-white flex items-center justify-center shadow-md shadow-purple-500/20">
                <mat-icon class="text-lg">format_list_bulleted</mat-icon>
              </div>
            </div>
            <mat-icon class="absolute -right-4 -bottom-4 text-[90px] text-purple-500/5 rotate-[-10deg] pointer-events-none">receipt</mat-icon>
          </div>
        </div>

        <!-- Filter Classification Tabs -->
        <div class="flex items-center justify-between flex-wrap gap-3">
          <div class="flex items-center gap-1.5 p-1 bg-gray-100 dark:bg-gray-800/60 rounded-xl text-xs font-semibold">
            <button 
              type="button"
              (click)="selectedGroup.set('ALL')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'ALL' ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              All ({{ data.lines.length }})
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Asset')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Asset' ? 'bg-white dark:bg-gray-700 text-blue-600 dark:text-blue-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              Assets
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Liability')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Liability' ? 'bg-white dark:bg-gray-700 text-amber-600 dark:text-amber-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              Liabilities
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Equity')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Equity' ? 'bg-white dark:bg-gray-700 text-purple-600 dark:text-purple-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              Equity
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Revenue')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Revenue' ? 'bg-white dark:bg-gray-700 text-emerald-600 dark:text-emerald-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              Revenue
            </button>
            <button 
              type="button"
              (click)="selectedGroup.set('Expense')"
              class="px-3 py-1.5 rounded-lg transition-all"
              [ngClass]="selectedGroup() === 'Expense' ? 'bg-white dark:bg-gray-700 text-rose-600 dark:text-rose-400 shadow-sm font-bold' : 'text-gray-500 dark:text-gray-400 hover:text-gray-900'">
              Expenses
            </button>
          </div>

          <div class="text-xs text-gray-500 dark:text-gray-400 font-medium">
            Showing {{ filteredLines().length }} of {{ data.lines.length }} accounts
          </div>
        </div>

        <!-- Main Trial Balance Table -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative print:border-none print:shadow-none">
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="border-b border-gray-200 dark:border-gray-700/60 bg-gray-50/80 dark:bg-gray-900/40 text-xs font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                  <th class="py-3.5 px-5">Code</th>
                  <th class="py-3.5 px-5">Account Name</th>
                  <th class="py-3.5 px-5">Classification</th>
                  <th class="py-3.5 px-5 text-right">Debit (BDT)</th>
                  <th class="py-3.5 px-5 text-right">Credit (BDT)</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100 dark:divide-gray-800/50 text-sm">
                <tr *ngFor="let line of filteredLines()" 
                    class="hover:bg-gray-50/50 dark:hover:bg-gray-700/20 transition-colors">
                  <td class="py-3.5 px-5 font-mono text-xs font-bold text-gray-600 dark:text-gray-400">
                    {{ line.accountCode }}
                  </td>
                  <td class="py-3.5 px-5 font-semibold text-gray-900 dark:text-white">
                    {{ line.accountName }}
                  </td>
                  <td class="py-3.5 px-5">
                    <span [ngClass]="getBadgeClass(line.categoryGroup)"
                          class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold">
                      {{ line.categoryGroup }}
                    </span>
                  </td>
                  <td class="py-3.5 px-5 text-right font-mono font-medium text-gray-800 dark:text-gray-200">
                    <span *ngIf="line.debitBdt > 0">{{ line.debitBdt | currency:'':'':'1.2-2' }}</span>
                    <span *ngIf="line.debitBdt === 0" class="text-gray-300 dark:text-gray-600">-</span>
                  </td>
                  <td class="py-3.5 px-5 text-right font-mono font-medium text-gray-800 dark:text-gray-200">
                    <span *ngIf="line.creditBdt > 0">{{ line.creditBdt | currency:'':'':'1.2-2' }}</span>
                    <span *ngIf="line.creditBdt === 0" class="text-gray-300 dark:text-gray-600">-</span>
                  </td>
                </tr>
              </tbody>
              <!-- Accounting Double Underline Totals Footer -->
              <tfoot>
                <tr class="border-t-2 border-b-4 border-gray-900 dark:border-gray-200 bg-gray-50 dark:bg-gray-900/60 font-black text-sm">
                  <td colspan="3" class="py-4 px-5 text-gray-900 dark:text-white uppercase tracking-wider flex items-center gap-2">
                    <span>Total Balances</span>
                    <span *ngIf="data.isBalanced" class="text-xs font-bold text-emerald-600 dark:text-emerald-400">✓ In Equilibrium</span>
                  </td>
                  <td class="py-4 px-5 text-right font-mono text-base font-black text-blue-700 dark:text-blue-400">
                    {{ data.totalDebitBdt | currency:'BDT ':'symbol':'1.2-2' }}
                  </td>
                  <td class="py-4 px-5 text-right font-mono text-base font-black text-emerald-700 dark:text-emerald-400">
                    {{ data.totalCreditBdt | currency:'BDT ':'symbol':'1.2-2' }}
                  </td>
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
export class TrialBalanceComponent {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);

  readonly selectedGroup = signal<'ALL' | 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense'>('ALL');

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

  private readonly tbData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filter, farm]) => {
      if (!farm) return of(null);
      return this.financeService.getTrialBalance(farm.id, filter.asOfDate).pipe(
        catchError(err => {
          console.error('Error loading trial balance:', err);
          return of(null);
        })
      );
    })
  );

  readonly tbData = toSignal(this.tbData$);
  readonly isLoading = computed(() => this.tbData() === undefined);

  readonly filteredLines = computed<TrialBalanceLine[]>(() => {
    const data = this.tbData();
    if (!data || !data.lines) return [];
    const group = this.selectedGroup();
    if (group === 'ALL') return data.lines;
    return data.lines.filter(l => l.categoryGroup.toLowerCase() === group.toLowerCase());
  });

  getBadgeClass(group: string): string {
    switch (group.toLowerCase()) {
      case 'asset':
        return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300';
      case 'liability':
        return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300';
      case 'equity':
        return 'bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300';
      case 'revenue':
        return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300';
      case 'expense':
        return 'bg-rose-100 dark:bg-rose-900/40 text-rose-700 dark:text-rose-300';
      default:
        return 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300';
    }
  }

  printReport(): void {
    window.print();
  }
}
