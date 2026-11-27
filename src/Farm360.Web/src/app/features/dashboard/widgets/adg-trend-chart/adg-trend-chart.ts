import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { RouterModule } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { AdgTrend } from '../../models/dashboard.model';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { translate } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-adg-trend-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, BaseChartDirective, RouterModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Top Metrics & Legend Bar -->
      <div class="flex flex-wrap items-center justify-between gap-2 pb-3 mb-2 border-b border-gray-100 dark:border-gray-700/60">
        <div class="flex flex-wrap items-baseline gap-x-2 min-w-0">
          <span class="text-xs text-gray-500 dark:text-gray-400">{{ 'kg per day · a gap means no weigh-in that month' | t }}</span>
        </div>

        <div *ngIf="hasData()" class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-primary-50 text-primary-800 dark:bg-primary-500/10 dark:text-primary-300 whitespace-nowrap">
          <mat-icon class="text-[14px] w-[14px] h-[14px]" aria-hidden="true">trending_up</mat-icon>
          {{ 'Avg: {value} kg/day' | t: { value: overallAvgAdg() } }}
        </div>
      </div>

      <!-- Chart Area -->
      <div *ngIf="hasData(); else emptyState" class="flex flex-col flex-1 justify-between">
        <div class="relative w-full h-[220px] sm:h-[250px]">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            [type]="chartType">
          </canvas>
        </div>
      </div>

      <!-- Empty State -->
      <ng-template #emptyState>
        <div class="flex flex-col items-center justify-center flex-1 py-10 text-center">
          <div class="w-12 h-12 rounded-full bg-gray-100 dark:bg-gray-700/50 text-gray-500 dark:text-gray-400 flex items-center justify-center mb-3" aria-hidden="true">
            <mat-icon>show_chart</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-800 dark:text-gray-200 m-0">{{ 'No ADG trends available' | t }}</p>
          <p class="text-sm text-gray-500 dark:text-gray-400 max-w-[260px] m-0 mt-1">{{ 'Record at least two weigh-ins per animal within a month to calculate its daily gain.' | t }}</p>
          <a routerLink="/livestock" class="mt-4 inline-flex items-center gap-1 min-h-[44px] px-3 rounded-lg text-sm font-semibold text-primary-700 dark:text-primary-400 hover:bg-primary-50 dark:hover:bg-primary-500/10 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            {{ 'Go to animals' | t }}
            <mat-icon class="text-[18px] w-[18px] h-[18px]" aria-hidden="true">arrow_forward</mat-icon>
          </a>
        </div>
      </ng-template>
    </div>
  `
})
export class AdgTrendChartComponent implements OnChanges {
  @Input() data: AdgTrend[] | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  private rawData = signal<AdgTrend[] | null>(null);

  public readonly chartType = 'line' as const;

  private readonly palette = [
    { border: '#8b5cf6', bg: 'rgba(139, 92, 246, 0.12)' },
    { border: '#6366f1', bg: 'rgba(99, 102, 241, 0.12)' },
    { border: '#f59e0b', bg: 'rgba(245, 158, 11, 0.12)' },
    { border: '#0284c7', bg: 'rgba(2, 132, 199, 0.12)' }
  ];

  public hasData = computed(() => {
    const list = this.rawData();
    return !!list && list.length > 0 && list.some(b => b.dataPoints?.some(p => p.adgValue !== null));
  });

  /** Mean of the measured months only; months without data are gaps, not zeros. */
  public overallAvgAdg = computed(() => {
    const list = this.rawData();
    if (!list || list.length === 0) return '0.00';
    let total = 0;
    let count = 0;
    for (const b of list) {
      for (const p of b.dataPoints) {
        if (p.adgValue === null) continue;
        total += p.adgValue;
        count++;
      }
    }
    return count > 0 ? (total / count).toFixed(2) : '0.00';
  });

  public chartData: ChartData<'line'> = {
    labels: [],
    datasets: []
  };

  public chartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: {
      mode: 'index',
      intersect: false
    },
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          usePointStyle: true,
          pointStyle: 'circle',
          padding: 16,
          font: { family: 'Inter, sans-serif', size: 12, weight: 500 }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(17, 24, 39, 0.95)',
        titleFont: { family: 'Inter, sans-serif', size: 12, weight: 'bold' },
        bodyFont: { family: 'Inter, sans-serif', size: 12 },
        padding: 10,
        cornerRadius: 10,
        callbacks: {
          label: (context) => {
            const label = context.dataset.label || '';
            if (context.parsed.y === null || context.parsed.y === undefined) {
              return ` ${label}: ${translate('no data')}`;
            }
            return ` ${label}: ${translate('{value} kg/day', { value: context.parsed.y.toFixed(2) })}`;
          }
        }
      }
    },
    scales: {
      x: {
        grid: {
          display: false
        },
        ticks: {
          font: { family: 'Inter, sans-serif', size: 11 }
        }
      },
      y: {
        beginAtZero: true,
        grid: {
          color: 'rgba(156, 163, 175, 0.12)'
        },
        ticks: {
          font: { family: 'Inter, sans-serif', size: 11 },
          callback: (value) => translate('{value} kg/d', { value })
        }
      }
    }
  };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.rawData.set(this.data);
      if (this.data && this.data.length > 0) {
        this.chartData = {
          labels: this.data[0].dataPoints.map(p => p.label),
          datasets: this.data.map((batch, index) => {
            const color = this.palette[index % this.palette.length];
            return {
              // null = no weigh-ins that month: drawn as a gap, never as an estimate.
              data: batch.dataPoints.map(p => p.adgValue),
              spanGaps: false,
              label: translate(batch.batchName),
              borderColor: color.border,
              backgroundColor: color.bg,
              fill: true,
              tension: 0.38,
              borderWidth: 2.5,
              pointRadius: 4,
              pointHoverRadius: 6,
              pointBackgroundColor: color.border,
              pointBorderColor: '#ffffff',
              pointBorderWidth: 1.5
            };
          })
        };
        this.chart?.update();
      }
    }
  }
}
