import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of, combineLatest, map } from 'rxjs';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { FinanceService } from '../../services/finance.service';
import { WorkingContextService } from '../../../../core/services/working-context.service';

@Component({
  selector: 'app-animal-cost-ledger',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe
  ],
  template: `
    <app-page-header 
      title="Unit Economics & Cost Ledger" 
      [description]="'Individual unit cost accumulation, break-even price, and profit margin targets for ' + (breakEvenData()?.tagId || animalId() || 'Animal')"
      breadcrumbActiveNode="Cost Ledger">
      <div actions class="flex items-center gap-3">
        <a mat-stroked-button routerLink="/livestock"
          class="rounded-xl border-gray-200 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-200 flex items-center gap-2 px-4 py-2">
          <mat-icon>pets</mat-icon>
          <span>Back to Livestock</span>
        </a>
        <a mat-flat-button routerLink="/finance/transactions"
          class="rounded-xl shadow-md bg-emerald-600 hover:bg-emerald-700 text-white flex items-center gap-2 px-4 py-2">
          <mat-icon>receipt_long</mat-icon>
          <span>View Ledger</span>
        </a>
      </div>
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="px-6 py-4 mx-auto max-w-7xl space-y-6">
      
      <app-empty-state 
        *ngIf="!ledgerData()"
        icon="request_quote"
        title="No Cost Ledger Found"
        description="Cost ledger for this animal has not been initialized or no costs exist.">
      </app-empty-state>

      <!-- Main Ledger View -->
      <div *ngIf="ledgerData() as ledger" class="space-y-6">
        
        <!-- Total Cost & Break-Even Hero Card -->
        <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative p-6 md:p-8">
          <div class="absolute -right-4 -bottom-4 text-[150px] text-emerald-500/5 rotate-[-10deg] pointer-events-none">
            <mat-icon inline="true">pets</mat-icon>
          </div>

          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-6 pb-6 border-b border-gray-100 dark:border-gray-700/50">
            <div>
              <div class="flex items-center gap-3">
                <span class="px-3 py-1 bg-emerald-100 dark:bg-emerald-950/50 text-emerald-800 dark:text-emerald-300 font-bold text-xs rounded-full uppercase">
                  Tag: {{ breakEvenData()?.tagId || 'N/A' }}
                </span>
                <span *ngIf="breakEvenData()?.currentWeightKg as weight" class="px-3 py-1 bg-blue-100 dark:bg-blue-950/50 text-blue-800 dark:text-blue-300 font-bold text-xs rounded-full">
                  Weight: {{ weight }} Kg
                </span>
              </div>
              <p class="text-xs font-bold uppercase tracking-wider text-gray-400 dark:text-gray-500 mt-3">Total Accumulated Cost of Ownership</p>
              <h2 class="text-4xl font-black text-gray-900 dark:text-white mt-1">
                {{ ledger.totalCostBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h2>
            </div>
            
            <div *ngIf="breakEvenData() as be" class="bg-gradient-to-br from-teal-500 to-emerald-600 text-white rounded-2xl p-5 shadow-lg shadow-emerald-500/20 flex items-center gap-5">
              <div class="w-14 h-14 rounded-xl bg-white/20 backdrop-blur-md flex items-center justify-center">
                <mat-icon class="text-3xl">scale</mat-icon>
              </div>
              <div>
                <p class="text-xs font-bold uppercase text-emerald-100 tracking-wider">Break-Even Price</p>
                <p class="text-3xl font-black">{{ be.breakEvenPricePerKgBdt | currency:'BDT ':'symbol':'1.0-2' }} <span class="text-sm font-medium text-emerald-100">/ Kg</span></p>
                <p class="text-[11px] text-emerald-100/80 mt-0.5">Minimum viable price to recover all costs</p>
              </div>
            </div>
          </div>

          <!-- Cost Breakdown Component Grid -->
          <div class="mt-6">
            <h3 class="text-sm font-bold uppercase tracking-wider text-gray-500 mb-4">Cost Center Allocation</h3>
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              
              <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/50">
                <div class="w-9 h-9 rounded-lg bg-indigo-50 dark:bg-indigo-950/40 text-indigo-600 flex items-center justify-center mb-2">
                  <mat-icon class="text-lg">shopping_cart</mat-icon>
                </div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Acquisition</p>
                <p class="text-xl font-bold text-gray-900 dark:text-white mt-1">{{ ledger.acquisitionCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>

              <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/50">
                <div class="w-9 h-9 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 flex items-center justify-center mb-2">
                  <mat-icon class="text-lg">grass</mat-icon>
                </div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Feed Consumed</p>
                <p class="text-xl font-bold text-gray-900 dark:text-white mt-1">{{ ledger.totalFeedCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>

              <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/50">
                <div class="w-9 h-9 rounded-lg bg-rose-50 dark:bg-rose-950/40 text-rose-600 flex items-center justify-center mb-2">
                  <mat-icon class="text-lg">medical_services</mat-icon>
                </div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Veterinary & Meds</p>
                <p class="text-xl font-bold text-gray-900 dark:text-white mt-1">{{ ledger.totalVetCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>

              <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700/60 bg-gray-50/50 dark:bg-gray-800/50">
                <div class="w-9 h-9 rounded-lg bg-amber-50 dark:bg-amber-950/40 text-amber-600 flex items-center justify-center mb-2">
                  <mat-icon class="text-lg">build</mat-icon>
                </div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Labor & Overhead</p>
                <p class="text-xl font-bold text-gray-900 dark:text-white mt-1">{{ (ledger.totalLaborCostBdt + ledger.totalOverheadBdt) | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>

            </div>
          </div>

          <!-- Realized Profit/Loss if sold -->
          <div *ngIf="ledger.saleRevenueBdt" class="mt-8 pt-6 border-t border-gray-100 dark:border-gray-700/50">
            <h3 class="text-sm font-bold uppercase tracking-wider text-gray-500 mb-4">Realized Financial Outcome</h3>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="p-5 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Realized Sale Revenue</p>
                <p class="text-2xl font-bold text-gray-900 dark:text-white mt-1">{{ ledger.saleRevenueBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>
              <div class="p-5 rounded-xl border" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'border-emerald-200 bg-emerald-50/70 dark:bg-emerald-950/30' : 'border-red-200 bg-red-50/70 dark:bg-red-950/30'">
                <p class="text-xs font-bold uppercase tracking-wider" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'">
                  {{ (ledger.profitLossBdt || 0) >= 0 ? 'Net Realized Profit' : 'Net Realized Loss' }}
                </p>
                <p class="text-2xl font-black mt-1" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'">
                  {{ Math.abs(ledger.profitLossBdt || 0) | currency:'BDT ':'symbol':'1.0-0' }}
                </p>
              </div>
            </div>
          </div>

        </div>

        <!-- Target Selling Price & Margin Tiers -->
        <div *ngIf="breakEvenData() as be" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 md:p-8">
          <div class="flex items-center justify-between mb-4">
            <div>
              <h3 class="text-lg font-bold text-gray-900 dark:text-white">Target Selling Price Tiers</h3>
              <p class="text-xs text-gray-400">Market pricing model calculated based on current live weight and accumulated cost</p>
            </div>
            <mat-icon class="text-emerald-600">price_check</mat-icon>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mt-6">
            
            <!-- +10% Margin Tier -->
            <div class="p-5 rounded-xl border border-teal-100 dark:border-teal-900/50 bg-teal-50/40 dark:bg-teal-950/20">
              <div class="flex items-center justify-between">
                <span class="px-2 py-0.5 bg-teal-100 dark:bg-teal-900/60 text-teal-800 dark:text-teal-300 text-xs font-bold rounded">
                  +10% Margin
                </span>
                <span class="text-xs text-teal-700 dark:text-teal-400 font-medium">Conservative</span>
              </div>
              <p class="text-2xl font-black text-teal-950 dark:text-teal-100 mt-3">
                {{ be.targetPrice10PercentMarginPerKg | currency:'BDT ':'symbol':'1.0-2' }} <span class="text-xs font-normal">/ Kg</span>
              </p>
              <p *ngIf="be.currentWeightKg as weight" class="text-xs text-gray-500 dark:text-gray-400 mt-2">
                Est. Total: <strong>{{ ((be.targetPrice10PercentMarginPerKg ?? 0) * weight) | currency:'BDT ':'symbol':'1.0-0' }}</strong>
              </p>
            </div>

            <!-- +20% Margin Tier -->
            <div class="p-5 rounded-xl border border-emerald-200 dark:border-emerald-800/60 bg-emerald-50/60 dark:bg-emerald-950/30 relative overflow-hidden">
              <div class="flex items-center justify-between">
                <span class="px-2 py-0.5 bg-emerald-600 text-white text-xs font-bold rounded">
                  +20% Margin (Recommended)
                </span>
                <span class="text-xs text-emerald-700 dark:text-emerald-400 font-medium">Optimal</span>
              </div>
              <p class="text-2xl font-black text-emerald-950 dark:text-emerald-100 mt-3">
                {{ be.targetPrice20PercentMarginPerKg | currency:'BDT ':'symbol':'1.0-2' }} <span class="text-xs font-normal">/ Kg</span>
              </p>
              <p *ngIf="be.currentWeightKg as weight" class="text-xs text-gray-500 dark:text-gray-400 mt-2">
                Est. Total: <strong>{{ ((be.targetPrice20PercentMarginPerKg ?? 0) * weight) | currency:'BDT ':'symbol':'1.0-0' }}</strong>
              </p>
            </div>

            <!-- +30% Margin Tier -->
            <div class="p-5 rounded-xl border border-purple-100 dark:border-purple-900/50 bg-purple-50/40 dark:bg-purple-950/20">
              <div class="flex items-center justify-between">
                <span class="px-2 py-0.5 bg-purple-100 dark:bg-purple-900/60 text-purple-800 dark:text-purple-300 text-xs font-bold rounded">
                  +30% Margin
                </span>
                <span class="text-xs text-purple-700 dark:text-purple-400 font-medium">Qurbani / Premium</span>
              </div>
              <p class="text-2xl font-black text-purple-950 dark:text-purple-100 mt-3">
                {{ be.targetPrice30PercentMarginPerKg | currency:'BDT ':'symbol':'1.0-2' }} <span class="text-xs font-normal">/ Kg</span>
              </p>
              <p *ngIf="be.currentWeightKg as weight" class="text-xs text-gray-500 dark:text-gray-400 mt-2">
                Est. Total: <strong>{{ ((be.targetPrice30PercentMarginPerKg ?? 0) * weight) | currency:'BDT ':'symbol':'1.0-0' }}</strong>
              </p>
            </div>

          </div>
        </div>

      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnimalCostLedgerComponent implements OnInit {
  private readonly financeService = inject(FinanceService);
  private readonly workingContextService = inject(WorkingContextService);
  private readonly route = inject(ActivatedRoute);

  readonly Math = Math;

  private readonly routeParams$ = this.route.paramMap.pipe(
    map(params => params.get('animalId'))
  );

  readonly animalId = toSignal(this.routeParams$);

  private readonly pageData$ = combineLatest([
    this.routeParams$,
    this.workingContextService.currentFarm$
  ]).pipe(
    switchMap(([animalId, farm]) => {
      if (!animalId || !farm) {
        return of({ ledger: null, breakEven: null });
      }
      return combineLatest({
        ledger: this.financeService.getAnimalCostLedger(farm.id, animalId).pipe(catchError(() => of(null))),
        breakEven: this.financeService.getBreakEven(farm.id, animalId).pipe(catchError(() => of(null)))
      });
    })
  );

  private readonly pageData = toSignal(this.pageData$);

  readonly ledgerData = computed(() => this.pageData()?.ledger);
  readonly breakEvenData = computed(() => this.pageData()?.breakEven);
  readonly isLoading = computed(() => this.pageData() === undefined);

  ngOnInit(): void {}
}
