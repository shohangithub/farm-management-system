import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { RouterModule } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { FeedCostTrend } from '../../models/dashboard.model';
import { MoneyPipe } from '../../../../shared/pipes/money.pipe';
import { formatMoney } from '../../../../shared/pipes/money.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { translate } from '../../../../core/i18n/i18n.service';

@Component({
  selector: 'app-feed-cost-trend-chart',
  standalone: true,
  imports: [MoneyPipe, CommonModule, MatIconModule, BaseChartDirective, RouterModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Top Metrics & Legend Bar -->
      <div class="flex flex-wrap items-center justify-between gap-2 pb-3 mb-2 border-b border-gray-100 dark:border-gray-700/60">
        <div class="flex flex-wrap items-baseline gap-x-2 min-w-0">
          <span class="text-xs text-gray-500 dark:text-gray-400">{{ 'Taka per animal, per month' | t }}</span>
        </div>

        <div *ngIf="hasData()" class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-primary-50 text-primary-800 dark:bg-primary-500/10 dark:text-primary-300 whitespace-nowrap">
          <mat-icon class="text-[14px] w-[14px] h-[14px]" aria-hidden="true">account_balance_wallet</mat-icon>
          {{ 'Latest: {value}' | t: { value: (latestCost() | money:2) } }}
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
            <mat-icon>restaurant</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-800 dark:text-gray-200 m-0">{{ 'No confirmed feeding records' | t }}</p>
          <p class="text-sm text-gray-500 dark:text-gray-400 max-w-[260px] m-0 mt-1">{{ 'Confirm daily feed logs to see monthly feed cost per head.' | t }}</p>
          <a routerLink="/feeding/today" class="mt-4 inline-flex items-center gap-1 min-h-[44px] px-3 rounded-lg text-sm font-semibold text-primary-700 dark:text-primary-400 hover:bg-primary-50 dark:hover:bg-primary-500/10 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500">
            {{ 'Open feed logs' | t }}
            <mat-icon class="text-[18px] w-[18px] h-[18px]" aria-hidden="true">arrow_forward</mat-icon>
          </a>
        </div>
      </ng-template>
    </div>
  `
})
export class FeedCostTrendChartComponent implements OnChanges {
  @Input() data: FeedCostTrend[] | null = null;
  @ViewChild(BaseChartDirective) chart: BaseChartDirective | undefined;

  private rawData = signal<FeedCostTrend[] | null>(null);

  public readonly chartType = 'bar' as const;

  public hasData = computed(() => {
    const list = this.rawData();
    return !!list && list.length > 0 && list.some(g => g.dataPoints?.some(p => p.costPerAnimal !== null));
  });

  public latestCost = computed((): number | null => {
    const list = this.rawData();
    if (!list || list.length === 0) return 0;
    const firstGroup = list[0];
    if (!firstGroup || firstGroup.dataPoints.length === 0) return 0;
    // Latest month that actually has data.
    const lastPoint = [...firstGroup.dataPoints].reverse().find(p => p.costPerAnimal !== null);
    return lastPoint && lastPoint.costPerAnimal !== null ? lastPoint.costPerAnimal : null;
  });

  public chartData: ChartData<'bar'> = {
    labels: [],
    datasets: []
  };

  public chartOptions: ChartConfiguration<'bar'>['options'] = {
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
          pointStyle: 'rectRounded',
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
            return ` ${label}: ${translate('{value} / head', { value: formatMoney(context.parsed.y) })}`;
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
          callback: (value) => `${formatMoney(Number(value))}`
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
          datasets: this.data.map((group, index) => ({
            data: group.dataPoints.map(p => p.costPerAnimal),
            label: translate(group.groupName),
            backgroundColor: 'rgba(99, 102, 241, 0.85)',
            hoverBackgroundColor: '#6366f1',
            borderRadius: 6,
            borderSkipped: false,
            barPercentage: 0.65,
            categoryPercentage: 0.6
          }))
        };
        this.chart?.update();
      }
    }
  }
}
