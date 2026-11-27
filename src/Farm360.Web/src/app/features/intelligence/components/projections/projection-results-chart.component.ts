import { Component, ChangeDetectionStrategy, input, effect, ElementRef, viewChild, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProfitProjectionResponse } from '../../models/projection.model';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import Chart from 'chart.js/auto';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { I18nService } from '../../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

const bdt = (v: number) => formatMoney(v);

@Component({
  selector: 'app-projection-results-chart',
  standalone: true,
  imports: [MoneyPipe, CommonModule, MatIconModule, MatTooltipModule, TranslatePipe],
  template: `
    <div class="bg-white/80 dark:bg-gray-800/80 backdrop-blur-xl rounded-2xl shadow-sm border border-gray-100 dark:border-gray-800/50 p-6 relative overflow-hidden h-full flex flex-col">
      <div class="flex items-center gap-3 mb-6">
        <div class="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-500/10 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
          <mat-icon class="material-icons-outlined">trending_up</mat-icon>
        </div>
        <div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white m-0">{{ 'Projection Trajectory' | t }}</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 m-0">{{ 'Total investment vs meat value, day by day' | t }}</p>
        </div>
      </div>

      <ng-container *ngIf="data()?.summary as summary">
        <!-- Headline KPIs at end of period -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
          <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
            <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">{{ 'Total Investment' | t }}</p>
            <p class="text-xl font-bold text-gray-900 dark:text-white">{{ summary.totalInvestmentBdt | money }}</p>
            <p class="text-[11px] text-gray-400 mt-1">{{ 'Start {start} + running {running}' | t: { start: (summary.purchaseCostBdt | money), running: (summary.totalFarmingCostBdt | money) } }}</p>
          </div>
          <div class="p-4 rounded-xl bg-gray-50 dark:bg-gray-700/50">
            <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">{{ 'Expected Sale Value' | t }}</p>
            <p class="text-xl font-bold text-gray-900 dark:text-white">{{ summary.expectedSaleValueBdt | money }}</p>
            <p class="text-[11px] text-gray-400 mt-1">{{ '{weight} kg meat × {price}' | t: { weight: (summary.finalMeatWeightKg | number:'1.1-1'), price: (summary.meatPriceUsedBdtPerKg | money:2) } }}</p>
          </div>
          <div class="p-4 rounded-xl" [ngClass]="isProfit(summary.profitLossBdt) ? 'bg-success-50 dark:bg-success-500/10' : 'bg-rose-50 dark:bg-rose-500/10'">
            <p class="text-xs uppercase tracking-wider mb-1" [ngClass]="isProfit(summary.profitLossBdt) ? 'text-success-600 dark:text-success-400' : 'text-rose-600 dark:text-rose-400'">
              {{ (isProfit(summary.profitLossBdt) ? 'Projected Profit' : 'Projected Loss') | t }}
            </p>
            <p class="text-xl font-bold" [ngClass]="isProfit(summary.profitLossBdt) ? 'text-success-600 dark:text-success-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.profitLossBdt > 0 ? '+' : '' }}{{ summary.profitLossBdt | money }}
            </p>
            <p class="text-[11px] text-gray-400 mt-1">{{ '≈ {amount} per day' | t: { amount: (summary.dailyProfitRateBdt | money) } }}</p>
          </div>
          <div class="p-4 rounded-xl bg-indigo-50 dark:bg-indigo-500/10">
            <p class="text-xs text-indigo-600 dark:text-indigo-400 uppercase tracking-wider mb-1">{{ 'ROI' | t }}</p>
            <!-- roiPercent is a ratio (0.12 = 12%); the percent pipe multiplies by 100. -->
            <p class="text-xl font-bold" [ngClass]="isProfit(summary.roiPercent) ? 'text-indigo-600 dark:text-indigo-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.roiPercent | percent:'1.1-2' }}
            </p>
            <p class="text-[11px] text-gray-400 mt-1">{{ 'Profit ÷ total investment' | t }}</p>
          </div>
        </div>

        <!-- Decision metrics -->
        <div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3 mb-6 text-sm">
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Final Weight' | t }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ summary.finalWeightKg | number:'1.1-1' }} {{ 'kg' | t }}</p>
            <p class="text-[11px] text-gray-400">{{ '+{weight} kg gain' | t: { weight: (summary.totalGainKg | number:'1.1-1') } }}</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Break-even Day' | t }}</p>
            <p class="font-semibold" [ngClass]="summary.breakEvenDay ? 'text-success-600 dark:text-success-400' : 'text-rose-600 dark:text-rose-400'">
              {{ summary.breakEvenDay ? ('Day {day}' | t: { day: summary.breakEvenDay }) : ('Not reached' | t) }}
            </p>
            <p class="text-[11px] text-gray-400">{{ 'First day value ≥ investment' | t }}</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Best Day to Sell' | t }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ 'Day {day}' | t: { day: summary.optimalSaleDay } }}</p>
            <p class="text-[11px]" [ngClass]="isProfit(summary.optimalProfitBdt ?? 0) ? 'text-success-600' : 'text-rose-600'">
              {{ (summary.optimalProfitBdt ?? 0) > 0 ? '+' : '' }}{{ summary.optimalProfitBdt | money }}
            </p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Break-even Meat Price' | t }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ summary.breakEvenPricePerMeatKgBdt | money:2 }}/{{ 'kg' | t }}</p>
            <p class="text-[11px] text-gray-400">{{ 'to sell on day {day}' | t: { day: lastDay() } }}</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Break-even Live Price' | t }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ summary.breakEvenPricePerLiveKgBdt | money:2 }}/{{ 'kg' | t }}</p>
            <p class="text-[11px] text-gray-400">{{ 'per kg live weight' | t }}</p>
          </div>
          <div class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
            <p class="text-[11px] text-gray-500 dark:text-gray-400">{{ 'Cost per kg Gain' | t }}</p>
            <p class="font-semibold text-gray-900 dark:text-white">{{ summary.costPerKgGainBdt | money:2 }}</p>
            <p class="text-[11px] text-gray-400">{{ '{weight} kg feed total' | t: { weight: (summary.totalFeedQtyKg | number:'1.0-0') } }}</p>
          </div>
        </div>

        <!-- Cost breakdown over the period -->
        <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500 dark:text-gray-400 mb-4">
          <span>{{ 'Feed' | t }} <b class="text-gray-800 dark:text-gray-200">{{ summary.totalFeedCostBdt | money }}</b></span>
          <span>{{ 'Grass' | t }} <b class="text-gray-800 dark:text-gray-200">{{ summary.totalGrassCostBdt | money }}</b></span>
          <span>{{ 'Other' | t }} <b class="text-gray-800 dark:text-gray-200">{{ summary.totalOtherCostBdt | money }}</b></span>
          <span>{{ 'Labour' | t }} <b class="text-gray-800 dark:text-gray-200">{{ summary.totalLaborCostBdt | money }}</b></span>
          <span>= {{ 'Running cost' | t }} <b class="text-gray-800 dark:text-gray-200">{{ summary.totalFarmingCostBdt | money }}</b></span>
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
  private readonly i18n = inject(I18nService);

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

    const labels = sampled.map(d => this.i18n.t('Day {day}', { day: d.day }));
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
            label: this.i18n.t('Total Investment'),
            data: costs,
            borderColor: '#f43f5e',
            backgroundColor: 'rgba(244, 63, 94, 0.08)',
            borderWidth: 2,
            pointRadius: 0,
            fill: false,
            tension: 0.2
          },
          {
            label: this.i18n.t('Meat Value'),
            data: values,
            borderColor: '#8b5cf6',
            backgroundColor: 'rgba(139, 92, 246, 0.08)',
            borderWidth: 2,
            pointRadius: 0,
            fill: false,
            tension: 0.2
          },
          {
            label: this.i18n.t('Profit / Loss'),
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
            title: { display: true, text: this.i18n.t('Investment / Value (BDT)') },
            ticks: { callback: v => bdt(Number(v)) }
          },
          y1: {
            position: 'right',
            title: { display: true, text: this.i18n.t('Profit / Loss (BDT)') },
            grid: { drawOnChartArea: false },
            ticks: { callback: v => bdt(Number(v)) }
          }
        }
      }
    });
  }
}
