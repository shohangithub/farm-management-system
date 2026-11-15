import { Component, ChangeDetectionStrategy, input, effect, ElementRef, viewChild, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProfitProjectionResponse } from '../../models/projection.model';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import Chart from 'chart.js/auto';

const bdt = (v: number) => '৳ ' + Math.round(v).toLocaleString('en-US');

@Component({
  selector: 'app-projection-results-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden h-full flex flex-col">
      <div class="flex items-center gap-3 mb-6">
        <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-500/10 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
          <mat-icon class="material-icons-outlined">trending_up</mat-icon>
        </div>
        <div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">Projection Trajectory</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 m-0">Total investment vs meat value, day by day</p>
        </div>
      </div>

      <ng-container *ngIf="data()?.summary as summary">
        <!-- Headline KPIs at end of period -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
          <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
            <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Total Investment</p>
            <p class="text-xl font-bold text-gray-900 dark:text-white">৳ {{ summary.totalInvestmentBdt | number:'1.0-0' }}</p>
            <p class="text-[11px] text-gray-400 mt-1">Start ৳{{ summary.purchaseCostBdt | number:'1.0-0' }} + running ৳{{ summary.totalFarmingCostBdt | number:'1.0-0' }}</p>
          </div>
          <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
            <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Expected Sale Value</p>
            <p class="text-xl font-bold text-gray-900 dark:text-white">৳ {{ summary.expectedSaleValueBdt | number:'1.0-0' }}</p>
            <p class="text-[11px] text-gray-400 mt-1">{{ summary.finalMeatWeightKg | number:'1.1-1' }} kg meat × ৳{{ summary.meatPriceUsedBdtPerKg | number:'1.0-2' }}</p>
          </div>
          <div class="p-4 rounded-xl" [ngClass]="isProfit(summary.profitLossBdt) ? 'bg-emerald-50 dark:bg-emerald-500/10' : 'bg-rose-50 dark:bg-rose-500/10'">
            <p class="text-xs uppercase tracking-wider mb-1" [ngClass]="isProfit(summary.profitLossBdt) ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              Projected {{ isProfit(summary.profitLossBdt) ? 'Profit' : 'Loss' }}
            </p>
            <p class="text-xl font-bold" [ngClass]="isProfit(summary.profitLossBdt) ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.profitLossBdt > 0 ? '+' : '' }}৳ {{ summary.profitLossBdt | number:'1.0-0' }}
            </p>
            <p class="text-[11px] text-gray-400 mt-1">≈ ৳{{ summary.dailyProfitRateBdt | number:'1.0-0' }} per day</p>
          </div>
          <div class="p-4 rounded-xl bg-indigo-50 dark:bg-indigo-500/10">
            <p class="text-xs text-indigo-600 dark:text-indigo-400 uppercase tracking-wider mb-1">ROI</p>
            <!-- roiPercent is a ratio (0.12 = 12%); the percent pipe multiplies by 100. -->
            <p class="text-xl font-bold" [ngClass]="isProfit(summary.roiPercent) ? 'text-indigo-600 dark:text-indigo-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.roiPercent | percent:'1.1-2' }}
            </p>
            <p class="text-[11px] text-gray-400 mt-1">Profit ÷ total investment</p>
          </div>
        </div>

        <!-- Decision metrics -->
        <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3 mb-6 text-sm">
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Final Weight</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ summary.finalWeightKg | number:'1.1-1' }} kg</p>
            <p class="text-[10px] text-gray-400">+{{ summary.totalGainKg | number:'1.1-1' }} kg gain</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Break-even Day</p>
            <p class="font-semibold" [ngClass]="summary.breakEvenDay ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.breakEvenDay ? 'Day ' + summary.breakEvenDay : 'Not reached' }}
            </p>
            <p class="text-[10px] text-gray-400">First day value ≥ investment</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Best Day to Sell</p>
            <p class="font-semibold text-gray-900 dark:text-white">Day {{ summary.optimalSaleDay }}</p>
            <p class="text-[10px]" [ngClass]="isProfit(summary.optimalProfitBdt ?? 0) ? 'text-emerald-600' : 'text-rose-600'">
              {{ (summary.optimalProfitBdt ?? 0) > 0 ? '+' : '' }}৳{{ summary.optimalProfitBdt | number:'1.0-0' }}
            </p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Break-even Meat Price</p>
            <p class="font-semibold text-gray-900 dark:text-white">৳ {{ summary.breakEvenPricePerMeatKgBdt | number:'1.0-2' }}/kg</p>
            <p class="text-[10px] text-gray-400">to sell on day {{ lastDay() }}</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Break-even Live Price</p>
            <p class="font-semibold text-gray-900 dark:text-white">৳ {{ summary.breakEvenPricePerLiveKgBdt | number:'1.0-2' }}/kg</p>
            <p class="text-[10px] text-gray-400">per kg live weight</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">Cost per kg Gain</p>
            <p class="font-semibold text-gray-900 dark:text-white">৳ {{ summary.costPerKgGainBdt | number:'1.0-2' }}</p>
            <p class="text-[10px] text-gray-400">{{ summary.totalFeedQtyKg | number:'1.0-0' }} kg feed total</p>
          </div>
        </div>

        <!-- Cost breakdown over the period -->
        <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500 dark:text-gray-400 mb-4">
          <span>Feed <b class="text-gray-800 dark:text-gray-200">৳{{ summary.totalFeedCostBdt | number:'1.0-0' }}</b></span>
          <span>Grass <b class="text-gray-800 dark:text-gray-200">৳{{ summary.totalGrassCostBdt | number:'1.0-0' }}</b></span>
          <span>Other <b class="text-gray-800 dark:text-gray-200">৳{{ summary.totalOtherCostBdt | number:'1.0-0' }}</b></span>
          <span>Labour <b class="text-gray-800 dark:text-gray-200">৳{{ summary.totalLaborCostBdt | number:'1.0-0' }}</b></span>
          <span>= Running cost <b class="text-gray-800 dark:text-gray-200">৳{{ summary.totalFarmingCostBdt | number:'1.0-0' }}</b></span>
        </div>
      </ng-container>

      <div class="flex-1 relative w-full" style="min-height: 300px;">
        <canvas #chartCanvas></canvas>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectionResultsChartComponent {
  data = input<ProfitProjectionResponse | null>(null);

  private chartCanvas = viewChild<ElementRef<HTMLCanvasElement>>('chartCanvas');
  private chartInstance: Chart | null = null;

  constructor() {
    // Runs once the canvas exists and again on every new projection.
    effect(() => {
      const projectionData = this.data();
      const canvas = this.chartCanvas();
      if (projectionData && canvas) this.updateChart(canvas.nativeElement, projectionData);
    });
    inject(DestroyRef).onDestroy(() => this.chartInstance?.destroy());
  }

  isProfit(v: number | null | undefined): boolean {
    return (v ?? 0) >= 0;
  }

  lastDay(): number {
    const days = this.data()?.days ?? [];
    return days.length ? days[days.length - 1].day : 0;
  }

  private updateChart(canvas: HTMLCanvasElement, data: ProfitProjectionResponse) {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Sample long periods so the chart stays readable, but always keep day 1, the last day and
    // the break-even day so the crossover is drawn where it really happens.
    const n = data.days.length;
    const step = Math.max(1, Math.ceil(n / 60));
    const breakEven = data.summary.breakEvenDay;
    const sampled = data.days.filter((d, i) => i % step === 0 || i === n - 1 || d.day === breakEven);

    const labels = sampled.map(d => `Day ${d.day}`);
    const costs = sampled.map(d => d.totalInvestmentBdt);
    const values = sampled.map(d => d.meatValueBdt);
    const profits = sampled.map(d => d.profitLossBdt);

    this.chartInstance?.destroy();
    this.chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Total Investment',
            data: costs,
            borderColor: '#f43f5e',
            backgroundColor: 'rgba(244, 63, 94, 0.08)',
            borderWidth: 2,
            pointRadius: 0,
            fill: false,
            tension: 0.2
          },
          {
            label: 'Meat Value',
            data: values,
            borderColor: '#10b981',
            backgroundColor: 'rgba(16, 185, 129, 0.08)',
            borderWidth: 2,
            pointRadius: 0,
            fill: false,
            tension: 0.2
          },
          {
            label: 'Profit / Loss',
            data: profits,
            borderColor: '#6366f1',
            borderDash: [5, 4],
            borderWidth: 1.5,
            pointRadius: 0,
            fill: false,
            tension: 0.2,
            yAxisID: 'y1'
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { position: 'top' },
          tooltip: {
            callbacks: {
              label: c => `${c.dataset.label}: ${c.parsed.y !== null ? bdt(c.parsed.y) : '—'}`
            }
          }
        },
        scales: {
          y: {
            beginAtZero: false,
            title: { display: true, text: 'Investment / Value (BDT)' },
            ticks: { callback: v => bdt(Number(v)) }
          },
          y1: {
            position: 'right',
            title: { display: true, text: 'Profit / Loss (BDT)' },
            grid: { drawOnChartArea: false },
            ticks: { callback: v => bdt(Number(v)) }
          }
        }
      }
    });
  }
}
