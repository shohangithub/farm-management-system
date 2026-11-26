import { Component, ChangeDetectionStrategy, input, signal, inject } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';

import { toLocalIsoDate } from '../../../../shared/utils/local-date';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
export interface SaleSimulationResult {
  animalId: string;
  targetDate: string;
  daysFromNow: number;
  projectedWeightKg: number;
  estimatedSalePriceBdt: number;
  projectedAdditionalCostBdt: number;
  projectedTotalCostBdt: number;
  projectedProfitMarginBdt: number;
}

@Component({
  selector: 'app-what-if-simulator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, CommonModule, DecimalPipe, MatIconModule, FormsModule, TranslatePipe],
  template: `
    <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-700 p-5 text-gray-900 dark:text-white relative overflow-hidden">
      
      <div class="relative z-10 flex flex-col gap-6">
        <div class="flex items-center justify-between">
          <div>
            <h3 class="text-base font-semibold text-gray-900 dark:text-white">{{ 'Sell on a later date' | t }}</h3>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-1 max-w-xl">
              {{ 'Pick a date to see the expected weight, extra cost and profit if you sell then.' | t }}
            </p>
          </div>
        </div>

        <div class="flex flex-col md:flex-row gap-6 items-end">
          <div class="w-full md:w-1/3">
            <label class="block text-xs text-gray-600 dark:text-gray-300 mb-1.5 font-medium">{{ 'Target Sale Date' | t }}</label>
            <input type="date"
                   class="w-full h-11 bg-white dark:bg-gray-900 border border-gray-300 dark:border-gray-600 rounded-xl px-3 text-gray-900 dark:text-white focus:outline-none focus:border-primary-500 focus:ring-2 focus:ring-primary-500/30 transition-all"
                   [ngModel]="targetDate()"
                   (ngModelChange)="onDateChange($event)" />
          </div>
          <button (click)="simulate()" 
                  [disabled]="isSimulating()"
                  class="h-11 bg-primary-600 hover:bg-primary-700 text-white font-semibold px-5 rounded-xl transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2">
            <mat-icon *ngIf="!isSimulating()">play_arrow</mat-icon>
            <mat-icon *ngIf="isSimulating()" class="animate-spin">sync</mat-icon>
            {{ (isSimulating() ? 'Calculating...' : 'Calculate') | t }}
          </button>
        </div>

        <div *ngIf="simulationResult() as result" class="grid grid-cols-1 md:grid-cols-4 gap-4 mt-4 animate-fade-in-up">
          <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
             <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Time Horizon' | t }}</p>
             <p class="text-xl font-bold text-gray-900 dark:text-white tabular-nums">+{{ '{n} Days' | t: { n: result.daysFromNow } }}</p>
          </div>
          <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
             <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Proj. Weight' | t }}</p>
             <p class="text-xl font-bold text-gray-900 dark:text-white tabular-nums">{{ result.projectedWeightKg | number:'1.1-1' }} {{ 'kg' | t }}</p>
          </div>
          <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
             <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Projected running cost' | t }}</p>
             <p class="text-xl font-bold text-gray-900 dark:text-white tabular-nums">{{ result.projectedAdditionalCostBdt | money }}</p>
             <p class="text-[11px] text-gray-400 dark:text-gray-500 mt-0.5">{{ 'feed + grass + other + labour' | t }}</p>
          </div>
          <div class="bg-emerald-50 dark:bg-emerald-900/20 rounded-xl p-3 border border-emerald-200 dark:border-emerald-800">
             <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1" [title]="'Projected value minus (cost to date + projected running cost)' | t">{{ 'Proj. Net Profit' | t }}</p>
             <p class="text-xl font-bold text-emerald-700 dark:text-emerald-400 flex items-center gap-1">
               <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">account_balance_wallet</mat-icon>
               {{ result.projectedProfitMarginBdt | money }}
             </p>
          </div>
        </div>

        <div *ngIf="error()" class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 p-4 rounded-xl text-sm flex items-start gap-2">
          <mat-icon class="text-red-500">error_outline</mat-icon>
          <p class="mt-0.5">{{ error() }}</p>
        </div>

      </div>
    </div>
  `
})
export class WhatIfSimulatorComponent {
  animalId = input.required<string>();
  
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);
  
  readonly targetDate = signal<string>(this.getTodayPlus30Days());
  readonly isSimulating = signal<boolean>(false);
  readonly simulationResult = signal<SaleSimulationResult | null>(null);
  readonly error = signal<string | null>(null);

  onDateChange(newDate: string) {
    this.targetDate.set(newDate);
  }

  simulate() {
    if (!this.animalId() || !this.targetDate()) return;
    
    this.isSimulating.set(true);
    this.error.set(null);
    this.simulationResult.set(null);

    const url = `/api/v1/intelligence/animals/${this.animalId()}/simulate-sale?targetDate=${this.targetDate()}`;
    
    this.http.get<SaleSimulationResult>(url)
      .subscribe({
        next: (data) => {
          this.simulationResult.set(data);
          this.isSimulating.set(false);
        },
        error: (err) => {
          this.error.set(err.error || this.i18n.t('Failed to run simulation. Ensure the animal has enough weight data.'));
          this.isSimulating.set(false);
        }
      });
  }

  private getTodayPlus30Days(): string {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return toLocalIsoDate(d);
  }
}
