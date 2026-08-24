import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe
  ],
  template: `
    <app-page-header 
      title="Financial Dashboard" 
      description="Month-to-date revenue, expenses, and net profit"
      breadcrumbActiveNode="Dashboard">
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>
    
    <div *ngIf="!isLoading()" class="p-6">
      <app-empty-state 
        *ngIf="!dashboardData()"
        icon="account_balance_wallet"
        title="No Financial Data"
        description="There is no financial data available for this farm yet.">
      </app-empty-state>

      <!-- Dashboard Cards Grid -->
      <div *ngIf="dashboardData() as data" class="grid grid-cols-1 md:grid-cols-3 gap-6">
        
        <!-- Revenue Card -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6">
          <div class="absolute -right-4 -bottom-4 text-[100px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">
            <mat-icon inline="true">trending_up</mat-icon>
          </div>
          <div class="flex justify-between items-start mb-4">
            <div>
              <p class="text-sm font-medium text-gray-500 dark:text-gray-400">Revenue (MTD)</p>
              <h3 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
                {{ data.revenueMtdBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h3>
            </div>
            <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white flex items-center justify-center shadow-md shadow-emerald-500/20">
              <mat-icon>account_balance</mat-icon>
            </div>
          </div>
          <div class="flex items-center text-sm">
            <span [ngClass]="data.revenueMomPercent >= 0 ? 'text-emerald-500 flex items-center' : 'text-red-500 flex items-center'">
              <mat-icon class="text-[16px] w-4 h-4 mr-1">{{ data.revenueMomPercent >= 0 ? 'arrow_upward' : 'arrow_downward' }}</mat-icon>
              {{ Math.abs(data.revenueMomPercent) }}%
            </span>
            <span class="text-gray-400 ml-2">vs last month</span>
          </div>
        </div>

        <!-- Expenses Card -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6">
          <div class="absolute -right-4 -bottom-4 text-[100px] text-rose-500/5 rotate-[-10deg] pointer-events-none">
            <mat-icon inline="true">trending_down</mat-icon>
          </div>
          <div class="flex justify-between items-start mb-4">
            <div>
              <p class="text-sm font-medium text-gray-500 dark:text-gray-400">Expenses (MTD)</p>
              <h3 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
                {{ data.expensesMtdBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h3>
            </div>
            <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-rose-500 to-red-600 text-white flex items-center justify-center shadow-md shadow-rose-500/20">
              <mat-icon>receipt_long</mat-icon>
            </div>
          </div>
          <div class="flex items-center text-sm">
            <span [ngClass]="data.expensesMomPercent <= 0 ? 'text-emerald-500 flex items-center' : 'text-red-500 flex items-center'">
              <mat-icon class="text-[16px] w-4 h-4 mr-1">{{ data.expensesMomPercent <= 0 ? 'arrow_downward' : 'arrow_upward' }}</mat-icon>
              {{ Math.abs(data.expensesMomPercent) }}%
            </span>
            <span class="text-gray-400 ml-2">vs last month</span>
          </div>
        </div>

        <!-- Net Profit Card -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6 md:col-span-1 lg:col-span-1">
          <div class="absolute -right-4 -bottom-4 text-[100px] text-blue-500/5 rotate-[-10deg] pointer-events-none">
            <mat-icon inline="true">savings</mat-icon>
          </div>
          <div class="flex justify-between items-start mb-4">
            <div>
              <p class="text-sm font-medium text-gray-500 dark:text-gray-400">Net Profit (MTD)</p>
              <h3 class="text-3xl font-bold mt-1" [ngClass]="data.netProfitMtdBdt >= 0 ? 'text-gray-900 dark:text-white' : 'text-red-600 dark:text-red-400'">
                {{ Math.abs(data.netProfitMtdBdt) | currency:'BDT ':'symbol':'1.0-0' }}
                <span *ngIf="data.netProfitMtdBdt < 0" class="text-sm font-normal">Loss</span>
              </h3>
            </div>
            <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center shadow-md shadow-blue-500/20">
              <mat-icon>account_balance_wallet</mat-icon>
            </div>
          </div>
          <div class="flex items-center text-sm">
            <span [ngClass]="data.netProfitMomPercent >= 0 ? 'text-emerald-500 flex items-center' : 'text-red-500 flex items-center'">
              <mat-icon class="text-[16px] w-4 h-4 mr-1">{{ data.netProfitMomPercent >= 0 ? 'arrow_upward' : 'arrow_downward' }}</mat-icon>
              {{ Math.abs(data.netProfitMomPercent) }}%
            </span>
            <span class="text-gray-400 ml-2">vs last month</span>
          </div>
        </div>

      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FinanceDashboardComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  
  Math = Math; // Template access
  private refreshTrigger$ = new BehaviorSubject<void>(undefined);

  private readonly dashboardData$ = this.refreshTrigger$.pipe(
    switchMap(() => {
      const currentFarmId = this.workingContextService.currentFarmValue?.id;
      if (!currentFarmId) {
        return of(null);
      }
      return this.financeService.getDashboard(currentFarmId).pipe(
        catchError(() => of(null))
      );
    })
  );

  readonly dashboardData = toSignal(this.dashboardData$);
  readonly isLoading = computed(() => this.dashboardData() === undefined);

  ngOnInit(): void {}
}
