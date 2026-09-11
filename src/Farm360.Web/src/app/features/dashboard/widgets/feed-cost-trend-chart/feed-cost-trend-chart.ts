import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { FeedCostTrend } from '../../models/dashboard.model';

@Component({
  selector: 'app-feed-cost-trend-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, BaseChartDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Top Metrics & Legend Bar -->
      <div class="flex items-center justify-between pb-3 mb-2 border-b border-gray-100 dark:border-gray-800/60">
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Unit:</span>
          <span class="text-xs font-medium text-gray-600 dark:text-gray-300">Cost per Head (BDT/Month)</span>
        </div>

        <div *ngIf="hasData()" class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-violet-50 text-violet-700 dark:bg-violet-950/40 dark:text-violet-400 border border-violet-200/50">
          <mat-icon class="text-[14px] w-[14px] h-[14px]">account_balance_wallet</mat-icon>
          Latest: ৳ {{ latestCost() }}
        </div>
      </div>

      <!-- Chart Area -->
      <div *ngIf="hasData(); else emptyState" class="flex flex-col flex-1 justify-between">
        <div class="relative w-full h-[250px]">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            [type]="chartType">
          </canvas>
        </div>
      </div>

      <!-- Empty State -->
      <ng-template #emptyState>
        <div class="flex flex-col items-center justify-center flex-1 py-12 text-center text-gray-400 dark:text-gray-500">
          <div class="w-14 h-14 rounded-2xl bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700/50 flex items-center justify-center mb-3">
            <mat-icon class="text-2xl text-gray-400">restaurant</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">No feed expense records</p>
          <p class="text-xs text-gray-400 max-w-[220px]">Confirm daily feed logs or feed purchase expenses to see cost trends.</p>
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
    return !!list && list.length > 0 && list.some(g => g.dataPoints && g.dataPoints.length > 0);
  });

  public latestCost = computed(() => {
    const list = this.rawData();
    if (!list || list.length === 0) return '0';
    const firstGroup = list[0];
    if (!firstGroup || firstGroup.dataPoints.length === 0) return '0';
    const lastPoint = firstGroup.dataPoints[firstGroup.dataPoints.length - 1];
    return lastPoint.costPerAnimal.toLocaleString();
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
            const val = context.parsed.y !== null ? context.parsed.y.toLocaleString() : '0';
            return ` ${label}: ৳ ${val} / head`;
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
          callback: (value) => `৳ ${Number(value).toLocaleString()}`
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
            label: group.groupName,
            backgroundColor: index === 0 ? 'rgba(99, 102, 241, 0.85)' : 'rgba(20, 184, 166, 0.55)',
            hoverBackgroundColor: index === 0 ? '#6366f1' : 'rgba(20, 184, 166, 0.85)',
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
