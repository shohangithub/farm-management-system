import { Component, ChangeDetectionStrategy, input, effect, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';

export interface AnimalFinancialSnapshot {
  animalId: string;
  totalInvestmentBdt: number;
  projected30DayFeedCostBdt: number;
  projected60DayFeedCostBdt: number;
  estimatedMarketValueBdt: number;
  currentProfitMarginBdt: number;
}

export interface AnimalIntelligenceData {
  activeInsights: ActionableInsight[];
  growthCurve?: GrowthCurve;
}

export interface ActionableInsight {
  id: string;
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
  imports: [CommonModule, DecimalPipe, MatIconModule],
  template: `
    <div class="space-y-6 animate-fade-in-up">
      <!-- Financial Projection Card -->
      <div *ngIf="snapshot()" class="bg-gradient-to-r from-emerald-900 to-teal-900 rounded-2xl shadow-lg border border-emerald-700/50 p-6 text-white relative overflow-hidden">
        <mat-icon class="absolute -right-4 -bottom-4 text-[120px] text-emerald-500/10 rotate-[-15deg] pointer-events-none">account_balance_wallet</mat-icon>
        
        <div class="relative z-10 flex flex-col lg:flex-row gap-6 items-center justify-between">
          <div class="flex-1 w-full">
            <div class="flex items-center gap-2 mb-2">
              <span class="px-2 py-0.5 text-[10px] uppercase tracking-wider font-bold rounded-md bg-emerald-500/20 text-emerald-300 border border-emerald-400/30">
                Smart Financials
              </span>
            </div>
            <h3 class="text-xl font-bold text-white tracking-tight">Financial Projection</h3>
            <p class="text-emerald-100/80 text-sm mt-1 max-w-md">Real-time cost & profit analysis based on live weight and feed history.</p>
          </div>

          <div class="flex-1 w-full grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="bg-white/10 backdrop-blur-md border border-white/10 rounded-xl p-4">
              <p class="text-[10px] text-emerald-200 uppercase tracking-wider font-semibold mb-1">Total Invested</p>
              <p class="text-xl font-bold text-white">৳ {{ snapshot()?.totalInvestmentBdt | number:'1.0-0' }}</p>
            </div>
            <div class="bg-white/10 backdrop-blur-md border border-white/10 rounded-xl p-4">
              <p class="text-[10px] text-emerald-200 uppercase tracking-wider font-semibold mb-1">Estimated Value</p>
              <p class="text-xl font-bold text-white">৳ {{ snapshot()?.estimatedMarketValueBdt | number:'1.0-0' }}</p>
            </div>
            <div class="bg-white/10 backdrop-blur-md border border-white/10 rounded-xl p-4"
                 [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'border-emerald-400/50 bg-emerald-500/20' : 'border-red-400/50 bg-red-500/20'">
              <p class="text-[10px] text-emerald-200 uppercase tracking-wider font-semibold mb-1">Net Profit Margin</p>
              <div class="flex items-center gap-1">
                <mat-icon class="!text-[16px] !w-[16px] !h-[16px]" [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'text-emerald-300' : 'text-red-300'">
                  {{ (snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'arrow_upward' : 'arrow_downward' }}
                </mat-icon>
                <p class="text-xl font-bold" [ngClass]="(snapshot()?.currentProfitMarginBdt ?? 0) >= 0 ? 'text-emerald-100' : 'text-red-100'">
                  ৳ {{ Math.abs(snapshot()?.currentProfitMarginBdt ?? 0) | number:'1.0-0' }}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Intelligence Data & Growth Projection -->
      <div *ngIf="intelData()" class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        
        <!-- Growth Projection Card -->
        <div *ngIf="intelData()?.growthCurve as curve" class="bg-gradient-to-br from-indigo-900 to-blue-900 rounded-2xl shadow-lg border border-indigo-700/50 p-6 text-white relative overflow-hidden">
          <mat-icon class="absolute -right-4 -bottom-4 text-[120px] text-indigo-500/10 rotate-[-15deg] pointer-events-none">trending_up</mat-icon>
          <div class="relative z-10">
            <div class="flex items-center gap-2 mb-2">
              <span class="px-2 py-0.5 text-[10px] uppercase tracking-wider font-bold rounded-md bg-indigo-500/20 text-indigo-300 border border-indigo-400/30">
                AI Growth Engine
              </span>
            </div>
            <h3 class="text-xl font-bold text-white tracking-tight mb-4">Growth Trajectory</h3>
            
            <div class="grid grid-cols-2 gap-4 mb-4">
               <div class="bg-black/20 rounded-xl p-4">
                 <p class="text-xs text-indigo-200 uppercase tracking-wide">Current ADG</p>
                 <p class="text-2xl font-bold mt-1">{{ curve.currentAdgKg | number:'1.2-2' }} <span class="text-sm font-normal text-indigo-300">kg/day</span></p>
               </div>
               <div class="bg-black/20 rounded-xl p-4">
                 <p class="text-xs text-indigo-200 uppercase tracking-wide">30-Day Projection</p>
                 <p class="text-2xl font-bold mt-1">{{ curve.projected30DayWeightKg | number:'1.1-1' }} <span class="text-sm font-normal text-indigo-300">kg</span></p>
               </div>
            </div>
          </div>
        </div>

        <!-- Actionable Insights Card -->
        <div *ngIf="intelData()?.activeInsights?.length" class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden">
          <mat-icon class="absolute -right-4 -bottom-4 text-[120px] text-amber-500/5 rotate-[-10deg] pointer-events-none">lightbulb</mat-icon>
          <div class="relative z-10">
            <h3 class="text-lg font-bold text-gray-900 dark:text-white mb-4 flex items-center gap-2">
              <mat-icon class="text-amber-500">tips_and_updates</mat-icon> Actionable Insights
            </h3>
            
            <div class="space-y-3">
              <div *ngFor="let insight of intelData()?.activeInsights" 
                   class="p-4 rounded-xl border flex gap-3"
                   [ngClass]="{
                     'bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-700/30': insight.severity === 'Warning',
                     'bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-700/30': insight.severity === 'Critical',
                     'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-700/30': insight.severity === 'Info',
                     'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-700/30': insight.severity === 'Success'
                   }">
                
                <mat-icon [ngClass]="{
                  'text-amber-500': insight.severity === 'Warning',
                  'text-red-500': insight.severity === 'Critical',
                  'text-blue-500': insight.severity === 'Info',
                  'text-emerald-500': insight.severity === 'Success'
                }">
                  {{ getInsightIcon(insight.severity) }}
                </mat-icon>
                
                <div>
                  <h4 class="font-bold text-sm" [ngClass]="{
                    'text-amber-900 dark:text-amber-200': insight.severity === 'Warning',
                    'text-red-900 dark:text-red-200': insight.severity === 'Critical',
                    'text-blue-900 dark:text-blue-200': insight.severity === 'Info',
                    'text-emerald-900 dark:text-emerald-200': insight.severity === 'Success'
                  }">{{ insight.title }}</h4>
                  <p class="text-sm mt-1" [ngClass]="{
                    'text-amber-800 dark:text-amber-300': insight.severity === 'Warning',
                    'text-red-800 dark:text-red-300': insight.severity === 'Critical',
                    'text-blue-800 dark:text-blue-300': insight.severity === 'Info',
                    'text-emerald-800 dark:text-emerald-300': insight.severity === 'Success'
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
