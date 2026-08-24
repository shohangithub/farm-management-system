import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
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
  selector: 'app-animal-cost-ledger',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    CurrencyPipe
  ],
  template: `
    <app-page-header 
      title="Animal Cost Ledger" 
      [description]="'Financial breakdown for animal ' + (animalId() || '')"
      breadcrumbActiveNode="Cost Ledger">
    </app-page-header>

    <app-loading *ngIf="isLoading()" [overlay]="true"></app-loading>

    <div *ngIf="!isLoading()" class="p-6 max-w-5xl mx-auto space-y-6">
      
      <app-empty-state 
        *ngIf="!ledgerData()"
        icon="request_quote"
        title="No Ledger Found"
        description="Cost ledger for this animal has not been initialized or no costs exist.">
      </app-empty-state>

      <!-- Main Ledger View -->
      <div *ngIf="ledgerData() as ledger" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 overflow-hidden relative">
        <div class="absolute -right-4 -bottom-4 text-[150px] text-gray-500/5 rotate-[-10deg] pointer-events-none">
          <mat-icon inline="true">pets</mat-icon>
        </div>

        <div class="p-6 md:p-8">
          <div class="flex flex-col md:flex-row md:items-end justify-between gap-4 mb-8 border-b border-gray-100 dark:border-gray-700/50 pb-6">
            <div>
              <p class="text-sm font-bold uppercase tracking-wider text-gray-500">Total Accumulated Cost</p>
              <h2 class="text-4xl font-bold text-gray-900 dark:text-white mt-1">
                {{ ledger.totalCostBdt | currency:'BDT ':'symbol':'1.0-0' }}
              </h2>
            </div>
            
            <div *ngIf="breakEvenData() as be" class="bg-blue-50 dark:bg-blue-900/20 border border-blue-100 dark:border-blue-800/50 rounded-xl p-4 flex items-center gap-4">
              <div class="w-10 h-10 rounded-full bg-blue-100 dark:bg-blue-800 flex items-center justify-center text-blue-600 dark:text-blue-300">
                <mat-icon>calculate</mat-icon>
              </div>
              <div>
                <p class="text-xs font-bold uppercase text-blue-600/70 dark:text-blue-400">Break-Even Price</p>
                <p class="text-lg font-bold text-blue-900 dark:text-blue-100">{{ be.breakEvenPricePerKgBdt | currency:'BDT ':'symbol':'1.0-2' }} / Kg</p>
              </div>
            </div>
          </div>

          <!-- Cost Breakdown Grid -->
          <h3 class="text-lg font-bold mb-4 text-gray-800 dark:text-gray-200">Cost Breakdown</h3>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            
            <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
              <mat-icon class="text-indigo-500 mb-2">shopping_cart</mat-icon>
              <p class="text-xs font-semibold text-gray-500 uppercase tracking-wider">Acquisition</p>
              <p class="text-lg font-bold text-gray-900 dark:text-white">{{ ledger.acquisitionCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
            </div>

            <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
              <mat-icon class="text-emerald-500 mb-2">grass</mat-icon>
              <p class="text-xs font-semibold text-gray-500 uppercase tracking-wider">Feed</p>
              <p class="text-lg font-bold text-gray-900 dark:text-white">{{ ledger.totalFeedCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
            </div>

            <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
              <mat-icon class="text-rose-500 mb-2">medical_services</mat-icon>
              <p class="text-xs font-semibold text-gray-500 uppercase tracking-wider">Veterinary</p>
              <p class="text-lg font-bold text-gray-900 dark:text-white">{{ ledger.totalVetCostBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
            </div>

            <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50">
              <mat-icon class="text-amber-500 mb-2">build</mat-icon>
              <p class="text-xs font-semibold text-gray-500 uppercase tracking-wider">Labor & Overhead</p>
              <p class="text-lg font-bold text-gray-900 dark:text-white">{{ (ledger.totalLaborCostBdt + ledger.totalOverheadBdt) | currency:'BDT ':'symbol':'1.0-0' }}</p>
            </div>

          </div>

          <!-- Profit/Loss if sold -->
          <div *ngIf="ledger.saleRevenueBdt" class="mt-8 pt-6 border-t border-gray-100 dark:border-gray-700/50">
            <h3 class="text-lg font-bold mb-4 text-gray-800 dark:text-gray-200">Outcome</h3>
            <div class="flex gap-4">
              <div class="p-4 rounded-xl border border-gray-100 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/50 flex-1">
                <p class="text-xs font-semibold text-gray-500 uppercase tracking-wider">Sale Revenue</p>
                <p class="text-lg font-bold text-gray-900 dark:text-white">{{ ledger.saleRevenueBdt | currency:'BDT ':'symbol':'1.0-0' }}</p>
              </div>
              <div class="p-4 rounded-xl border flex-1" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'border-emerald-200 bg-emerald-50 dark:bg-emerald-900/20' : 'border-red-200 bg-red-50 dark:bg-red-900/20'">
                <p class="text-xs font-semibold uppercase tracking-wider" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'">
                  {{ (ledger.profitLossBdt || 0) >= 0 ? 'Profit' : 'Loss' }}
                </p>
                <p class="text-xl font-bold" [ngClass]="(ledger.profitLossBdt || 0) >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'">
                  {{ Math.abs(ledger.profitLossBdt || 0) | currency:'BDT ':'symbol':'1.0-0' }}
                </p>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnimalCostLedgerComponent implements OnInit {
  private financeService = inject(FinanceService);
  private workingContextService = inject(WorkingContextService);
  private route = inject(ActivatedRoute);

  Math = Math;

  private routeParams$ = this.route.paramMap.pipe(
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
