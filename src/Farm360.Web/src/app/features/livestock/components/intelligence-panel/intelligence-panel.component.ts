import { Component, ChangeDetectionStrategy, input, effect, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { IntelligenceSignalRService } from '../../../../core/services/intelligence-signalr.service';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface AnimalFinancialSnapshot {
  animalId: string;
  totalInvestmentBdt: number;
  projected30DayCostBdt: number;
  projected60DayCostBdt: number;
  estimatedMarketValueBdt: number;
  currentProfitMarginBdt: number;
}

export interface AnimalIntelligenceData {
  activeInsights: ActionableInsight[];
  growthCurve?: GrowthCurve;
}

export interface ActionableInsight {
  id: string;
  animalId?: string;
  type: string;
  severity: string;
  title: string;
  message: string;
  createdOnUtc: string;
}

export interface GrowthCurve {
  currentWeightKg: number;
  projected30DayWeightKg: number;
  projected60DayWeightKg: number;
  projected90DayWeightKg: number;
  currentAdgKg: number;
}

@Component({
  selector: 'app-intelligence-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, CommonModule, DecimalPipe, MatIconModule, TranslatePipe],
  template: `
    <div class="space-y-6 animate-fade-in-up">
      <!-- Financial Projection Card -->
      <div *ngIf="snapshot()" class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-700 p-5 text-gray-900 dark:text-white relative overflow-hidden">
        
        <div class="relative z-10 flex flex-col lg:flex-row gap-6 items-center justify-between">
          <div class="flex-1 w-full">
            <h3 class="text-base font-semibold text-gray-900 dark:text-white">{{ 'Value today' | t }}</h3>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-1 max-w-md">{{ 'Based on the latest weight and all costs so far.' | t }}</p>
          </div>

          <div class="flex-1 w-full grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
              <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Total Invested' | t }}</p>
              <p class="text-xl font-bold text-gray-900 dark:text-white tabular-nums">{{ snapshot()?.totalInvestmentBdt | money }}</p>
            </div>
            <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
              <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Estimated Value' | t }}</p>
              <p class="text-xl font-bold text-gray-900 dark:text-white tabular-nums">{{ snapshot()?.estimatedMarketValueBdt | money }}</p>
            </div>
            <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3"
                 [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? '' : ''">
              <p class="text-[11px] text-gray-500 dark:text-gray-400 mb-1">{{ 'Profit if sold today' | t }}</p>
              <div class="flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'text-success-600 dark:text-success-400' : 'text-red-600 dark:text-red-400'">
                  {{ (snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'arrow_upward' : 'arrow_downward' }}
                </mat-icon>
                <p class="text-xl font-bold" [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'text-success-700 dark:text-success-400' : 'text-red-700 dark:text-red-400'">
                  {{ Math.abs(snapshot()?.currentProfitMarginBdt ?? 0) | money }}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Intelligence Data & Growth Projection -->
      <div *ngIf="intelData()" class="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
        
        <!-- Growth Projection Card -->
        <div *ngIf="intelData()?.growthCurve as curve" class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-700 p-5 text-gray-900 dark:text-white relative overflow-hidden h-full">
          <div class="relative z-10">
            <h3 class="text-base font-semibold text-gray-900 dark:text-white mb-4">{{ 'Expected weight' | t }}</h3>
            
            <div class="grid grid-cols-2 gap-4 mb-2">
               <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
                 <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Current ADG' | t }}</p>
                 <p class="text-2xl font-bold mt-1">{{ curve.currentAdgKg | number:'1.2-2' }} <span class="text-xs font-normal text-gray-500 dark:text-gray-400">{{ 'kg/day' | t }}</span></p>
               </div>
               <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
                 <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ '30-Day Projection' | t }}</p>
                 <p class="text-2xl font-bold mt-1">{{ curve.projected30DayWeightKg | number:'1.1-1' }} <span class="text-xs font-normal text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span></p>
               </div>
               <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
                 <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ '60-Day Projection' | t }}</p>
                 <p class="text-xl font-bold mt-1">{{ curve.projected60DayWeightKg | number:'1.1-1' }} <span class="text-xs font-normal text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span></p>
               </div>
               <div class="bg-gray-50 dark:bg-gray-900/50 rounded-xl p-3">
                 <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ '90-Day Projection' | t }}</p>
                 <p class="text-xl font-bold mt-1">{{ curve.projected90DayWeightKg | number:'1.1-1' }} <span class="text-xs font-normal text-gray-500 dark:text-gray-400">{{ 'kg' | t }}</span></p>
               </div>
            </div>
          </div>
        </div>

        <!-- Actionable Insights Card -->
        <div *ngIf="intelData()?.activeInsights?.length" class="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-200 dark:border-gray-700 p-5 text-gray-900 dark:text-white relative overflow-hidden">
          <div class="relative z-10">
            <h3 class="text-lg font-bold text-gray-900 dark:text-white mb-4 flex items-center gap-2">
              <mat-icon class="text-amber-500">tips_and_updates</mat-icon> {{ 'Things to check' | t }}
            </h3>
            
            <div class="space-y-3">
              <div *ngFor="let insight of intelData()?.activeInsights" 
                   class="p-4 rounded-xl border flex gap-3 animate-fade-in-up"
                   [ngClass]="{
                     'bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-700/30': insight.severity === 'Warning',
                     'bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-700/30': insight.severity === 'Critical',
                     'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-700/30': insight.severity === 'Info',
                     'bg-success-50 dark:bg-success-900/20 border-success-200 dark:border-success-700/30': insight.severity === 'Success'
                   }">
                
                <mat-icon [ngClass]="{
                  'text-amber-500': insight.severity === 'Warning',
                  'text-red-500': insight.severity === 'Critical',
                  'text-blue-500': insight.severity === 'Info',
                  'text-success-500': insight.severity === 'Success'
                }">
                  {{ getInsightIcon(insight.severity) }}
                </mat-icon>
                
                <div>
                  <h4 class="font-bold text-sm" [ngClass]="{
                    'text-amber-900 dark:text-amber-200': insight.severity === 'Warning',
                    'text-red-900 dark:text-red-200': insight.severity === 'Critical',
                    'text-blue-900 dark:text-blue-200': insight.severity === 'Info',
                    'text-success-900 dark:text-success-200': insight.severity === 'Success'
                  }">{{ insight.title }}</h4>
                  <p class="text-sm mt-1" [ngClass]="{
                    'text-amber-800 dark:text-amber-300': insight.severity === 'Warning',
                    'text-red-800 dark:text-red-300': insight.severity === 'Critical',
                    'text-blue-800 dark:text-blue-300': insight.severity === 'Info',
                    'text-success-800 dark:text-success-300': insight.severity === 'Success'
                  }">{{ insight.message }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>
    </div>
  `
})
export class IntelligencePanelComponent {
  animalId = input.required<string>();

  private readonly http = inject(HttpClient);
  private readonly signalR = inject(IntelligenceSignalRService);

  readonly snapshot = signal<AnimalFinancialSnapshot | null>(null);
  readonly intelData = signal<AnimalIntelligenceData | null>(null);

  readonly Math = Math;

  constructor() {
    effect(() => {
      const id = this.animalId();
      if (id) {
        this.loadSnapshot(id);
        this.loadIntelligenceData(id);
      }
    });

    effect(() => {
      const insight = this.signalR.latestInsight();
      if (insight && insight.animalId === this.animalId()) {
        const currentData = this.intelData();
        if (currentData) {
          // Add the new insight to the top of the list if it's not already there
          if (!currentData.activeInsights.some(x => x.id === insight.id)) {
            // Clone the object to trigger change detection
            this.intelData.set({
              ...currentData,
              activeInsights: [insight, ...currentData.activeInsights]
            });
          }
        }
      }
    }, { allowSignalWrites: true });
  }

  private loadSnapshot(id: string) {
    this.http.get<AnimalFinancialSnapshot>(`/api/v1/intelligence/animals/${id}/financial-snapshot`)
      .subscribe({
        next: (data) => this.snapshot.set(data),
        error: (err) => console.error('Failed to load intelligence snapshot', err)
      });
  }

  private loadIntelligenceData(id: string) {
    this.http.get<AnimalIntelligenceData>(`/api/v1/intelligence/animals/${id}/data`)
      .subscribe({
        next: (data) => this.intelData.set(data),
        error: (err) => console.error('Failed to load intelligence data', err)
      });
  }

  getInsightIcon(severity: string): string {
    switch (severity) {
      case 'Critical': return 'error';
      case 'Warning': return 'warning';
      case 'Success': return 'check_circle';
      default: return 'info';
    }
  }
}
