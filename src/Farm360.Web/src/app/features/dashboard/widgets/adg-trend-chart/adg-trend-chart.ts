import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { AdgTrend } from '../../models/dashboard.model';

@Component({
  selector: 'app-adg-trend-chart',
  standalone: true,
  imports: [CommonModule, MatIconModule, BaseChartDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col h-full">
      <!-- Top Metrics & Legend Bar -->
      <div class="flex items-center justify-between pb-3 mb-2 border-b border-gray-100 dark:border-gray-800/60">
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-gray-400 uppercase tracking-wider">Metric:</span>
          <span class="text-xs font-medium text-gray-600 dark:text-gray-300">Average Daily Gain (kg/day)</span>
        </div>

        <div *ngIf="hasData()" class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/50">
          <mat-icon class="text-[14px] w-[14px] h-[14px]">trending_up</mat-icon>
          Avg: {{ overallAvgAdg() }} kg/day
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
            <mat-icon class="text-2xl text-gray-400">show_chart</mat-icon>
          </div>
          <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">No ADG trends available</p>
          <p class="text-xs text-gray-400 max-w-[220px]">Log consecutive weight records to calculate growth velocity.</p>
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
    { border: '#10b981', bg: 'rgba(16, 185, 129, 0.12)' },
    { border: '#6366f1', bg: 'rgba(99, 102, 241, 0.12)' },
    { border: '#f59e0b', bg: 'rgba(245, 158, 11, 0.12)' },
    { border: '#0284c7', bg: 'rgba(2, 132, 199, 0.12)' }
  ];

  public hasData = computed(() => {
    const list = this.rawData();
    return !!list && list.length > 0 && list.some(b => b.dataPoints && b.dataPoints.length > 0);
  });

  public overallAvgAdg = computed(() => {
    const list = this.rawData();
    if (!list || list.length === 0) return '0.00';
    let total = 0;
    let count = 0;
    for (const b of list) {
      for (const p of b.dataPoints) {
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
            const val = context.parsed.y !== null ? context.parsed.y.toFixed(2) : '0.00';
            return ` ${label}: ${val} kg/day`;
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
          callback: (value) => `${value} kg/d`
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
              data: batch.dataPoints.map(p => p.adgValue),
              label: batch.batchName,
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
