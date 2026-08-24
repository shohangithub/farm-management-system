import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, BehaviorSubject } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-monthly-pnl-report',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe
  ],
  template: `
    <app-page-header 
      title="Monthly Profit & Loss" 
      description="Detailed breakdown of income and expenses by category for a specific month."
      breadcrumbActiveNode="Monthly P&L">
      <div actions class="flex items-center gap-4">
        <form [formGroup]="filterForm" class="flex items-center gap-2">
          <select formControlName="year" class="px-3 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-sm font-medium focus:ring-2 focus:ring-primary-500 outline-none">
            <option *ngFor="let y of years" [value]="y">{{ y }}</option>
          </select>
          <select formControlName="month" class="px-3 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-700 rounded-xl text-sm font-medium focus:ring-2 focus:ring-primary-500 outline-none">
            <option *ngFor="let m of months" [value]="m.value">{{ m.label }}</option>
          </select>
        </form>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-6xl mx-auto space-y-6">
      
      <app-empty-state 
        *ngIf="!reportData()"
        icon="event_note"
        title="No Data Available"
        description="There are no financial records for the selected month.">
      </app-empty-state>

      <!-- Main Report View -->
      <div *ngIf="reportData() as report" class="space-y-6">
        
        <!-- KPIs -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between">
            <div>
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Total Income</p>
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white mt-1">
                {{ report.totalIncomeBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h2>
            </div>
            <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <mat-icon>arrow_downward</mat-icon>
            </div>
          </div>

          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between">
            <div>
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Total Expense</p>
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white mt-1">
                {{ report.totalExpenseBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h2>
            </div>
            <div class="w-12 h-12 rounded-full bg-rose-100 dark:bg-rose-900/30 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <mat-icon>arrow_upward</mat-icon>
            </div>
          </div>

          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 flex items-center justify-between relative overflow-hidden"
               [ngClass]="report.netProfitBdt >= 0 ? 'bg-gradient-to-br from-emerald-50 to-teal-50 dark:from-emerald-900/10 dark:to-teal-900/10' : 'bg-gradient-to-br from-rose-50 to-red-50 dark:from-rose-900/10 dark:to-red-900/10'">
            <div class="relative z-10">
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Net Profit</p>
              <h2 class="text-2xl font-bold mt-1" [ngClass]="report.netProfitBdt >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'">
                {{ Math.abs(report.netProfitBdt) | currency:'BDT ':'symbol':'1.0-0' }}
                <span *ngIf="report.netProfitBdt < 0" class="text-sm font-normal">Loss</span>
              </h2>
            </div>
            <div class="absolute right-[-10px] bottom-[-10px] opacity-20">
              <mat-icon class="!w-[80px] !h-[80px] !text-[80px]" [ngClass]="report.netProfitBdt >= 0 ? 'text-emerald-500' : 'text-rose-500'">
                {{ report.netProfitBdt >= 0 ? 'trending_up' : 'trending_down' }}
              </mat-icon>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          
          <!-- Income Breakdown -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6">
            <div class="flex items-center gap-2 mb-6 pb-4 border-b border-gray-100 dark:border-gray-700">
              <div class="w-8 h-8 rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <mat-icon class="!w-4 !h-4 !text-[16px]">add</mat-icon>
              </div>
              <h3 class="text-lg font-bold text-gray-900 dark:text-white">Income by Category</h3>
            </div>
            
            <div class="space-y-4">
              <div *ngIf="getCategoryKeys(report.incomeByCategory).length === 0" class="text-sm text-gray-500 italic">No income recorded.</div>
              <div *ngFor="let key of getCategoryKeys(report.incomeByCategory)" class="flex items-center justify-between">
                <span class="text-sm font-medium text-gray-600 dark:text-gray-300">{{ formatCategory(key) }}</span>
                <span class="text-sm font-bold text-gray-900 dark:text-white">{{ report.incomeByCategory[key] | currency:'BDT ':'symbol':'1.0-0' }}</span>
              </div>
            </div>
          </div>

          <!-- Expense Breakdown -->
          <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6">
            <div class="flex items-center gap-2 mb-6 pb-4 border-b border-gray-100 dark:border-gray-700">
              <div class="w-8 h-8 rounded-full bg-rose-100 dark:bg-rose-900/30 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                <mat-icon class="!w-4 !h-4 !text-[16px]">remove</mat-icon>
              </div>
              <h3 class="text-lg font-bold text-gray-900 dark:text-white">Expense by Category</h3>
            </div>
            
            <div class="space-y-4">
              <div *ngIf="getCategoryKeys(report.expenseByCategory).length === 0" class="text-sm text-gray-500 italic">No expenses recorded.</div>
              <div *ngFor="let key of getCategoryKeys(report.expenseByCategory)" class="flex items-center justify-between">
                <span class="text-sm font-medium text-gray-600 dark:text-gray-300">{{ formatCategory(key) }}</span>
                <span class="text-sm font-bold text-gray-900 dark:text-white">{{ report.expenseByCategory[key] | currency:'BDT ':'symbol':'1.0-0' }}</span>
              </div>
            </div>
          </div>

        </div>
      </div>

    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MonthlyPnlReportComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private fb = inject(FormBuilder);

  Math = Math;

  readonly currentYear = new Date().getFullYear();
  readonly years = [this.currentYear - 2, this.currentYear - 1, this.currentYear, this.currentYear + 1];
  
  readonly months = [
    { value: 1, label: 'January' },
    { value: 2, label: 'February' },
    { value: 3, label: 'March' },
    { value: 4, label: 'April' },
    { value: 5, label: 'May' },
    { value: 6, label: 'June' },
    { value: 7, label: 'July' },
    { value: 8, label: 'August' },
    { value: 9, label: 'September' },
    { value: 10, label: 'October' },
    { value: 11, label: 'November' },
    { value: 12, label: 'December' }
  ];

  filterForm!: FormGroup;
  private filterChanges$ = new BehaviorSubject<{ year: number, month: number }>({
    year: this.currentYear,
    month: new Date().getMonth() + 1
  });

  private readonly reportData$ = combineLatest([
    this.filterChanges$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([filters, farm]) => {
      if (!farm) {
        return of(null);
      }
      return this.financeService.getMonthlyPnL(farm.id, filters.year, filters.month).pipe(
        catchError(() => of(null))
      );
    })
  );

  readonly reportData = toSignal(this.reportData$);
  readonly isLoading = computed(() => this.reportData() === undefined);

  ngOnInit(): void {
    this.filterForm = this.fb.group({
      year: [this.filterChanges$.value.year],
      month: [this.filterChanges$.value.month]
    });

    this.filterForm.valueChanges.subscribe(val => {
      this.filterChanges$.next({
        year: Number(val.year),
        month: Number(val.month)
      });
    });
  }

  getCategoryKeys(dict: { [key: string]: number }): string[] {
    if (!dict) return [];
    return Object.keys(dict).sort();
  }

  formatCategory(key: string): string {
    // Convert CamelCase to Space Case
    return key.replace(/([A-Z])/g, ' $1').trim();
  }
}
