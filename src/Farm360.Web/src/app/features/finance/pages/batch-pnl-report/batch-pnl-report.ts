import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, PercentPipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, map } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-batch-pnl-report',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe,
    PercentPipe
  ],
  template: `
    <app-page-header 
      title="Batch Profit & Loss" 
      [description]="'Financial performance for batch ' + (batchId() || '')"
      breadcrumbActiveNode="Batch P&L">
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-5xl mx-auto space-y-6">
      
      <app-empty-state 
        *ngIf="!reportData()"
        icon="trending_flat"
        title="No P&L Data"
        description="No financial transactions found for this batch.">
      </app-empty-state>

      <!-- Main Report View -->
      <div *ngIf="reportData() as report" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6 md:p-8">
        <div class="absolute -right-4 -bottom-4 text-[150px] text-gray-500/5 rotate-[-10deg] pointer-events-none">
          <mat-icon inline="true">leaderboard</mat-icon>
        </div>

        <div class="flex flex-col md:flex-row gap-6 mb-8 border-b border-gray-100 dark:border-gray-700/50 pb-8">
          <!-- Total Income -->
          <div class="flex-1">
            <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Total Income</p>
            <h2 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
              {{ report.totalIncomeBdt | currency:'BDT ':'symbol':'1.0-0' }}
            </h2>
          </div>

          <!-- Total Cost -->
          <div class="flex-1 border-l-0 md:border-l border-gray-100 dark:border-gray-700/50 md:pl-6">
            <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Total Cost</p>
            <h2 class="text-3xl font-bold text-gray-900 dark:text-white mt-1">
              {{ report.totalCostBdt | currency:'BDT ':'symbol':'1.0-0' }}
            </h2>
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          
          <!-- Gross Profit -->
          <div class="bg-gray-50/50 dark:bg-gray-800/50 rounded-xl p-6 border border-gray-100 dark:border-gray-700 flex items-center justify-between">
            <div>
              <p class="text-xs font-bold uppercase tracking-wider text-gray-500 mb-1">Gross Profit</p>
              <h3 class="text-2xl font-bold" [ngClass]="report.grossProfitBdt >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'">
                {{ Math.abs(report.grossProfitBdt) | currency:'BDT ':'symbol':'1.0-0' }}
                <span *ngIf="report.grossProfitBdt < 0" class="text-sm">Loss</span>
              </h3>
            </div>
            <div class="w-12 h-12 rounded-full flex items-center justify-center" [ngClass]="report.grossProfitBdt >= 0 ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-600 dark:text-emerald-400' : 'bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400'">
              <mat-icon>{{ report.grossProfitBdt >= 0 ? 'trending_up' : 'trending_down' }}</mat-icon>
            </div>
          </div>

          <!-- ROI -->
          <div class="bg-blue-50/50 dark:bg-blue-900/20 rounded-xl p-6 border border-blue-100 dark:border-blue-800/50 flex items-center justify-between">
            <div>
              <p class="text-xs font-bold uppercase tracking-wider text-blue-600/70 dark:text-blue-400 mb-1">Return on Investment (ROI)</p>
              <h3 class="text-2xl font-bold text-blue-900 dark:text-blue-100">
                {{ report.returnOnInvestmentPercent / 100 | percent:'1.2-2' }}
              </h3>
            </div>
            <div class="w-12 h-12 rounded-full bg-blue-100 dark:bg-blue-800/50 text-blue-600 dark:text-blue-300 flex items-center justify-center">
              <mat-icon>percent</mat-icon>
            </div>
          </div>

        </div>

        <div class="mt-6 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-800/30 p-3 rounded-lg border border-gray-100 dark:border-gray-700/50">
          <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">info</mat-icon>
          <span>This batch contains <strong>{{ report.totalAnimals }}</strong> animals. ROI is calculated as (Gross Profit / Total Cost) * 100.</span>
        </div>

      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BatchPnlReportComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private route = inject(ActivatedRoute);

  Math = Math;

  private routeParams$ = this.route.paramMap.pipe(
    map(params => params.get('batchId'))
  );

  readonly batchId = toSignal(this.routeParams$);

  private readonly reportData$ = combineLatest([
    this.routeParams$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([batchId, farm]) => {
      if (!batchId || !farm) {
        return of(null);
      }
      return this.financeService.getBatchPnL(farm.id, batchId).pipe(catchError(() => of(null)));
    })
  );

  readonly reportData = toSignal(this.reportData$);
  readonly isLoading = computed(() => this.reportData() === undefined);

  ngOnInit(): void {}
}
